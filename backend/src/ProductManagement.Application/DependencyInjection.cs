using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ProductManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<Categories.CreateCategoryHandler>();
        services.AddScoped<Categories.UpdateCategoryHandler>();
        services.AddScoped<Categories.DeleteCategoryHandler>();
        services.AddScoped<Categories.GetCategoryHandler>();
        services.AddScoped<Categories.ListCategoriesHandler>();

        services.AddScoped<Products.CreateProductHandler>();
        services.AddScoped<Products.UpdateProductHandler>();
        services.AddScoped<Products.DeleteProductHandler>();
        services.AddScoped<Products.GetProductHandler>();
        services.AddScoped<Products.ListProductsHandler>();
        services.AddScoped<Products.UploadProductImageHandler>();
        services.AddScoped<Products.DeleteProductImageHandler>();

        services.AddScoped<Variants.CreateVariantHandler>();
        services.AddScoped<Variants.UpdateVariantHandler>();
        services.AddScoped<Variants.DeleteVariantHandler>();
        services.AddScoped<Variants.ListVariantsHandler>();
        services.AddScoped<Variants.AdjustStockHandler>();

        services.AddScoped<Variations.ListVariationsHandler>();
        services.AddScoped<Variations.CreateVariationHandler>();
        services.AddScoped<Variations.CreateVariationOptionHandler>();

        services.AddScoped<Promotions.ListPromotionsHandler>();
        services.AddScoped<Promotions.CreatePromotionHandler>();
        services.AddScoped<Promotions.AttachPromotionCategoryHandler>();

        return services;
    }
}
