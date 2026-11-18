using DocumentManagement.Application.Abstractions;
using DocumentManagement.Infrastructure.Data;
using DocumentManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "The 'DefaultConnection' connection string is not configured.");

        services.AddDbContext<DocumentDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        return services;
    }
}
