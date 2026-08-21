using ProductManagement.Api.Middleware;
using ProductManagement.Application;
using ProductManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(); // must be early in the pipeline, before routing/controllers
app.UseStaticFiles(); // serves wwwroot/uploads at /uploads (spec section 10)

app.MapControllers();

app.Run();

public partial class Program { } // exposed for WebApplicationFactory<Program> in IntegrationTests
