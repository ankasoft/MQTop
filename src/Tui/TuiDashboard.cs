using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Rendering;
using MQTop.Configuration;
using MQTop.Services;
using MQTop.Tui.Panels;

namespace MQTop.Tui;

public class TuiDashboard : BackgroundService
{
    private readonly DashboardState _state;
    private readonly DashboardOptions _dashboardOptions;
    private readonly AlertsOptions _alertsOptions;
    private readonly IHostApplicationLifetime _lifetime;

    private bool _showConnectionLog;
    private int _scrollOffset;
    private string _vendorFilter = "All";
    private readonly string[] _filterCycle;
    private int _filterIndex;

    public TuiDashboard(
        DashboardState state,
        IOptions<DashboardOptions> dashboardOptions,
        IOptions<AlertsOptions> alertsOptions,
        IHostApplicationLifetime lifetime)
    {
        _state = state;
        _dashboardOptions = dashboardOptions.Value;
        _alertsOptions = alertsOptions.Value;
        _lifetime = lifetime;

        var filters = new List<string> { "All" };
        filters.AddRange(_dashboardOptions.Vendors);
        _filterCycle = filters.ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            _lifetime.StopApplication();
        };

        _ = Task.Run(() => InputLoop(stoppingToken), stoppingToken);

        try
        {
            await AnsiConsole.Live(BuildLayout())
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    var interval = TimeSpan.FromMilliseconds(_dashboardOptions.RefreshIntervalMs);
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        ctx.UpdateTarget(BuildLayout());
                        try
                        {
                            await Task.Delay(interval, stoppingToken);
                        }
                        catch (OperationCanceledException) { break; }
                    }
                });
        }
        catch (OperationCanceledException) { /* graceful shutdown */ }
    }

    private IRenderable BuildLayout()
    {
        var alerts = AlertsPanel.Collect(_state, _alertsOptions);

        var layout = new Layout("root")
            .SplitRows(
                new Layout("header").Size(3),
                new Layout("brokerRow").Size(15),
                new Layout("vendors").Size(_state.Vendors.Count == 0 ? 4 : Math.Max(5, _state.Vendors.Count + 4)),
                new Layout("terminals").Ratio(2),
                new Layout("alerts").Size(Math.Min(7, Math.Max(3, alerts.Count + 2))),
                new Layout("footer").Size(1));

        layout["header"].Update(HeaderPanel.Render(_state, _vendorFilter));

        if (_showConnectionLog)
        {
            layout["brokerRow"].SplitColumns(
                new Layout("broker").Update(BrokerPanel.Render(_state, _alertsOptions)),
                new Layout("load").Update(LoadPanel.Render(_state)),
                new Layout("log").Update(ConnectionLogPanel.Render(_state)));
        }
        else
        {
            layout["brokerRow"].SplitColumns(
                new Layout("broker").Update(BrokerPanel.Render(_state, _alertsOptions)),
                new Layout("load").Update(LoadPanel.Render(_state)));
        }

        layout["vendors"].Update(VendorPanel.Render(_state, _vendorFilter));
        layout["terminals"].Update(TerminalPanel.Render(_state, _vendorFilter, _scrollOffset));
        layout["alerts"].Update(AlertsPanel.Render(alerts));
        layout["footer"].Update(FooterPanel.Render());

        return layout;
    }

    private void InputLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(50);
                    continue;
                }

                var key = Console.ReadKey(intercept: true);
                switch (key.Key)
                {
                    case ConsoleKey.Q:
                        _lifetime.StopApplication();
                        return;
                    case ConsoleKey.L:
                        _showConnectionLog = !_showConnectionLog;
                        break;
                    case ConsoleKey.V:
                        if (_filterCycle.Length > 0)
                        {
                            _filterIndex = (_filterIndex + 1) % _filterCycle.Length;
                            _vendorFilter = _filterCycle[_filterIndex];
                            _scrollOffset = 0;
                        }
                        break;
                    case ConsoleKey.R:
                        _state.ResetCounters();
                        break;
                    case ConsoleKey.UpArrow:
                        _scrollOffset = Math.Max(0, _scrollOffset - 1);
                        break;
                    case ConsoleKey.DownArrow:
                        _scrollOffset++;
                        break;
                    case ConsoleKey.PageUp:
                        _scrollOffset = Math.Max(0, _scrollOffset - 10);
                        break;
                    case ConsoleKey.PageDown:
                        _scrollOffset += 10;
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                // No console (redirected) — exit input loop.
                return;
            }
            catch
            {
                // best-effort — ignore transient input errors
            }
        }
    }
}
