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

        return services;
    }
}
