using ProductManagement.Api.Middleware;
using ProductManagement.Application;
using ProductManagement.Infrastructure;
using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON logging to console (spec section 6) - this is also what
// GlobalExceptionHandler's ILogger<T> writes through for the 500/traceId
// case (Task 11), since UseSerilog() replaces the default logging provider.
builder.Host.UseSerilog((context, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console(new JsonFormatter()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173" })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("ETag", "Location"));
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(); // must be early in the pipeline, before routing/controllers
app.UseCors("Frontend"); // must be after routing/exception handling, before MapControllers
app.UseStaticFiles(); // serves wwwroot/uploads at /uploads (spec section 10)

app.MapControllers();

app.Run();

public partial class Program { } // exposed for WebApplicationFactory<Program> in IntegrationTests
