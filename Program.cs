using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTop.Configuration;
using MQTop.Services;
using MQTop.Tui;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "MQTOP_")
    .AddCommandLine(args);

builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection("Dashboard"));
builder.Services.Configure<AlertsOptions>(builder.Configuration.GetSection("Alerts"));

builder.Services.AddSingleton(sp =>
{
    var opts = builder.Configuration.GetSection("Dashboard").Get<DashboardOptions>() ?? new DashboardOptions();
    return new DashboardState(opts);
});

builder.Logging.ClearProviders();
// TUI takes over stdout; log to debug only.
builder.Logging.AddDebug();

builder.Services.AddHostedService<MqttMonitorService>();
builder.Services.AddHostedService<TuiDashboard>();

var host = builder.Build();
await host.RunAsync();
