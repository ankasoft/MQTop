using Spectre.Console;
using Spectre.Console.Rendering;
using MQTop.Configuration;
using MQTop.Models;
using MQTop.Services;
using MQTop.Tui.Helpers;

namespace MQTop.Tui.Panels;

public static class AlertsPanel
{
    private static long _lastDropped;
    private static int _rotationTick;

    public static List<Alert> Collect(DashboardState state, AlertsOptions opts)
    {
        var alerts = new List<Alert>();
        var b = state.Broker;

        if (!b.IsConnected)
        {
            alerts.Add(new Alert
            {
                Level = AlertLevel.Critical,
                Message = "Broker disconnected, reconnecting..."
            });
        }

        var prevDropped = Interlocked.Read(ref _lastDropped);
        if (b.MessagesDropped > prevDropped && b.MessagesDropped >= opts.DroppedMessagesThreshold)
        {
            alerts.Add(new Alert
            {
                Level = AlertLevel.Critical,
                Message = $"Messages being dropped! Total: {FormatHelper.Number(b.MessagesDropped)}"
            });
        }
        Interlocked.Exchange(ref _lastDropped, b.MessagesDropped);

        if (b.LoadMsgDropped1Min > 0)
        {
            alerts.Add(new Alert
            {
                Level = AlertLevel.Critical,
                Message = $"Messages dropped in last 1m: {FormatHelper.Load(b.LoadMsgDropped1Min)}/s"
            });
        }

        if (b.MessagesStored > opts.StoredMessagesThreshold)
        {
            alerts.Add(new Alert
            {
                Level = AlertLevel.Warning,
                Message = $"Offline queue: {FormatHelper.Number(b.MessagesStored)} pending messages"
            });
        }

        if (b.MessagesInflight > opts.InflightMessagesThreshold)
        {
            alerts.Add(new Alert
            {
                Level = AlertLevel.Warning,
                Message = $"{FormatHelper.Number(b.MessagesInflight)} messages may be stuck inflight"
            });
        }

        foreach (var v in state.Vendors.Values)
        {
            if (Interlocked.Read(ref v.TotalCmdCount) < 5) continue;
            var rate = v.SuccessRate;
            if (rate < opts.SuccessRateCriticalThreshold)
            {
                alerts.Add(new Alert
                {
                    Level = AlertLevel.Critical,
                    Message = $"{v.Name} success rate critical: {FormatHelper.Percent(rate)}"
                });
            }
            else if (rate < opts.SuccessRateWarningThreshold)
            {
                alerts.Add(new Alert
                {
                    Level = AlertLevel.Warning,
                    Message = $"{v.Name} success rate low: {FormatHelper.Percent(rate)}"
                });
            }
        }

        var now = DateTime.UtcNow;
        foreach (var t in state.GetAllTerminals())
        {
            if (string.Equals(t.Status, "online", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.LastSeen == default) continue;
            if (now - t.LastSeen > TimeSpan.FromHours(1))
            {
                alerts.Add(new Alert
                {
                    Level = AlertLevel.Warning,
                    Message = $"{t.SerialNo} ({t.Vendor}) offline for 1+ hour"
                });
            }
        }

        return alerts;
    }

    public static IRenderable Render(IReadOnlyList<Alert> alerts)
    {
        if (alerts.Count == 0)
        {
            return new Panel(new Markup("[green]All systems normal[/]"))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green)
                .Header("[bold]Alerts[/]");
        }

        const int windowSize = 3;
        List<Alert> visible;
        if (alerts.Count <= windowSize)
        {
            visible = alerts.ToList();
        }
        else
        {
            var tick = Interlocked.Increment(ref _rotationTick);
            // Slow rotation: change window every ~5 ticks (5 seconds at 1s refresh).
            var start = (tick / 5) % alerts.Count;
            visible = Enumerable.Range(0, windowSize)
                .Select(i => alerts[(start + i) % alerts.Count])
                .ToList();
        }

        var rows = new Rows(visible.Select(a =>
        {
            var styled = a.Level == AlertLevel.Critical
                ? $"[red bold]✖ {Markup.Escape(a.Message)}[/]"
                : $"[yellow]⚠ {Markup.Escape(a.Message)}[/]";
            return (IRenderable)new Markup(styled);
        }).ToArray());

        var hasCritical = alerts.Any(a => a.Level == AlertLevel.Critical);
        return new Panel(rows)
            .Border(BoxBorder.Rounded)
            .BorderColor(hasCritical ? Color.Red : Color.Yellow)
            .Header($"[bold]Alerts ({alerts.Count})[/]");
    }
}
