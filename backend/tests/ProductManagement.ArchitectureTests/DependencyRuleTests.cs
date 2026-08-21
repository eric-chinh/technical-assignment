using FluentAssertions;
using NetArchTest.Rules;
using ProductManagement.Api.Controllers;
using ProductManagement.Application;
using ProductManagement.Domain.Entities;
using ProductManagement.Infrastructure;
using Xunit;

namespace ProductManagement.ArchitectureTests;

public class DependencyRuleTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(Product).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly ApiAssembly = typeof(CategoriesController).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Other_Layers_Or_Frameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
                "ProductManagement.Application", "ProductManagement.Infrastructure", "ProductManagement.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Api_Or_Frameworks()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
                "ProductManagement.Infrastructure", "ProductManagement.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOn("ProductManagement.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Controllers_Should_Not_Depend_On_Infrastructure_Directly()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("ProductManagement.Api.Controllers")
            .Should()
            .NotHaveDependencyOn("ProductManagement.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    private static string FailureMessage(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>());
}
