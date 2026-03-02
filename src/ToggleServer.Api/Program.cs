using FluentValidation;
using Microsoft.OpenApi;
using Serilog;
using ToggleServer.Api.Endpoints;
using ToggleServer.Api.Middleware;
using ToggleServer.Api.Services;
using ToggleServer.Core.Interfaces;
using ToggleServer.Core.Models;
using ToggleServer.Infrastructure;

// 1. 設定 Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    // 套用 Serilog 設定
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // 註冊基礎結構服務 (MongoDB, Repository)
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // 註冊業務邏輯與驗證
    builder.Services.AddScoped<IToggleService, ToggleService>();
    builder.Services.AddValidatorsFromAssemblyContaining<ToggleServer.Api.Validators.CreateToggleRequestValidator>();

    // Mock Authorization (Minimal API endpoints uses .RequireAuthorization())
    builder.Services.AddAuthorization();
    
    // Add Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "Toggle Server Management API",
            Version = "v1"
        });
        
        // Add security definition so user can input "Bearer mock-token" in Swagger UI
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Enter 'Bearer mock-token' here."
        });
        
        // options.AddSecurityRequirement(new OpenApiSecurityRequirement
        // {
        //     {
        //         new Microsoft.OpenApi.OpenApiSecurityScheme
        //         {
        //             Reference = new Microsoft.OpenApi.OpenApiReference
        //             {
        //                 Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
        //                 Id = "Bearer"
        //             }
        //         },
        //         new string[] {}
        //     }
        // });
    });

    var app = builder.Build();

    // Enable Swagger
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Toggle Server API v1"));
    }

    app.UseSerilogRequestLogging();

    // 掛載我們自訂的 Mock Auth Middleware (會在路由之前抓緊驗證)
    app.UseMiddleware<MockAuthMiddleware>();

    app.UseAuthorization();

    // 註冊 Endpoints
    app.MapToggleEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// 供 Integration Test 參照
public partial class Program { }
