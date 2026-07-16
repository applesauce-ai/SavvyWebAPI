using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Savvy.Application.Common;
using Savvy.Application.Notifications;
using Savvy.Infrastructure.Notifications;
using Savvy.Infrastructure.Persistence;

namespace Savvy.Infrastructure;

/// <summary>
/// Registration entry point for the Infrastructure layer (EF Core, notifications, etc.).
/// Keeps Program.cs thin and the persistence wiring in one place.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SavvyDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SavvyDb' was not found in configuration.");

        services.AddDbContext<SavvyDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(SavvyDbContext).Assembly.FullName)));

        // Expose the context through its application-layer abstraction.
        services.AddScoped<ISavvyDbContext>(sp => sp.GetRequiredService<SavvyDbContext>());

        services.AddNotifications(configuration);

        return services;
    }

    private static void AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(NotificationsOptions.SectionName);
        services.Configure<NotificationsOptions>(section);

        // Select the transport by configuration: Discord (local) or Teams (Azure).
        var provider = section.GetValue<string>("Provider") ?? "Discord";
        if (string.Equals(provider, "Teams", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<IWebhookNotifier, TeamsWebhookNotifier>();
        else
            services.AddHttpClient<IWebhookNotifier, DiscordWebhookNotifier>();

        services.AddScoped<INotificationService, NotificationService>();
    }
}
