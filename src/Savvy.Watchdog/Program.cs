using System.Reflection;
using Savvy.Watchdog;
using Savvy.Watchdog.Alerting;

// Pin the content root to the app's own directory so appsettings.json is always found — even when
// the exe is launched from a different working directory (not just via `dotnet run`).
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Load the webhook secret from user-secrets regardless of environment. Host.CreateApplicationBuilder
// only adds user-secrets in Development; adding it explicitly means the watchdog finds its Discord
// webhook whether launched via `dotnet run`, the built exe, or any environment. (In Azure the
// watchdog isn't used; secrets there would come from Key Vault / environment variables.)
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

builder.Services.AddOptions<WatchdogOptions>()
    .Bind(builder.Configuration.GetSection(WatchdogOptions.SectionName));

// Named client for health probes; typed client for the Discord notifier.
builder.Services.AddHttpClient("health");
builder.Services.AddHttpClient<IAlertNotifier, DiscordWebhookNotifier>();

builder.Services.AddHostedService<HealthMonitorWorker>();

var host = builder.Build();
host.Run();
