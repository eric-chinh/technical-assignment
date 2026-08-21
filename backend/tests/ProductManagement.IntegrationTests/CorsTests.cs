using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class CorsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public CorsTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PreflightRequest_FromAllowedOrigin_ReturnsAllowOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/categories");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("http://localhost:5173");
    }

    [Fact]
    public async Task ActualRequest_FromAllowedOrigin_ExposesETagHeader()
    {
        // Access-Control-Expose-Headers is only meaningful on the actual response, not the
        // preflight OPTIONS response - the preflight only negotiates whether the request is
        // allowed at all, not which response headers get exposed to JS. Confirmed against a
        // real request/response pair, not asserted on the preflight.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/categories");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await _client.SendAsync(request);

        var exposedHeaders = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
        exposedHeaders.Should().Contain("ETag");
    }
}
