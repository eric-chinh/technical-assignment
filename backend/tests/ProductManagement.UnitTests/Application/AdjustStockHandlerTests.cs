using FluentAssertions;
using NSubstitute;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Variants;
using Xunit;

namespace ProductManagement.UnitTests.Application;

public class AdjustStockHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenRepositorySucceeds_ReturnsSuccessResultAndInvalidatesCache()
    {
        var stock = Substitute.For<IStockRepository>();
        var cache = Substitute.For<ICacheService>();
        stock.TryAdjustAsync(42, -3, Arg.Any<CancellationToken>())
            .Returns(new StockAdjustResult(true, NewQuantity: 7, AvailableQuantity: null));

        var handler = new AdjustStockHandler(stock, cache);
        var result = await handler.HandleAsync(productId: 1, variantId: 42, new AdjustStockRequest(-3), idempotencyKey: null, default);

        result.Succeeded.Should().BeTrue();
        result.NewQuantity.Should().Be(7);
        await cache.Received(1).RemoveAsync("product:1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryReportsInsufficientStock_ReturnsConflictResultAndDoesNotInvalidateCache()
    {
        var stock = Substitute.For<IStockRepository>();
        var cache = Substitute.For<ICacheService>();
        stock.TryAdjustAsync(42, -10, Arg.Any<CancellationToken>())
            .Returns(new StockAdjustResult(false, NewQuantity: null, AvailableQuantity: 3));

        var handler = new AdjustStockHandler(stock, cache);
        var result = await handler.HandleAsync(productId: 1, variantId: 42, new AdjustStockRequest(-10), idempotencyKey: null, default);

        result.Succeeded.Should().BeFalse();
        result.AvailableQuantity.Should().Be(3);
        await cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithIdempotencyKeySeenBefore_ReturnsCachedResultWithoutTouchingStockRepository()
    {
        var stock = Substitute.For<IStockRepository>();
        var cache = Substitute.For<ICacheService>();
        var cachedResult = new AdjustStockResult(true, NewQuantity: 7, AvailableQuantity: null);
        cache.GetAsync<AdjustStockResult>("stock-adjust:key-123", Arg.Any<CancellationToken>()).Returns(cachedResult);

        var handler = new AdjustStockHandler(stock, cache);
        var result = await handler.HandleAsync(productId: 1, variantId: 42, new AdjustStockRequest(-3), "key-123", default);

        result.Should().Be(cachedResult);
        await stock.DidNotReceive().TryAdjustAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
