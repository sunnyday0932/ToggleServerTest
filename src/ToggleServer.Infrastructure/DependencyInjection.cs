using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using ToggleServer.Core.Interfaces;
using ToggleServer.Infrastructure.Data;

namespace ToggleServer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDbSettings"));
        
        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>();
            return new MongoClient(settings?.ConnectionString ?? "mongodb://localhost:27017");
        });

        services.AddScoped<IFeatureToggleRepository, MongoFeatureToggleRepository>();
        
        return services;
    }
}
