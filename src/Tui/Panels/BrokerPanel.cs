using Spectre.Console;
using Spectre.Console.Rendering;
using MQTop.Configuration;
using MQTop.Services;
using MQTop.Tui.Helpers;

namespace MQTop.Tui.Panels;

public static class BrokerPanel
{
    public static IRenderable Render(DashboardState state, AlertsOptions alerts)
    {
        var b = state.Broker;

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("k").NoWrap())
            .AddColumn(new TableColumn("v"));

        table.AddRow("[grey]Version[/]", $"[white]{Markup.Escape(b.Version)}[/]");
        table.AddRow("[grey]Uptime[/]", $"[white]{Markup.Escape(FormatHelper.UptimeFromSeconds(b.Uptime))}[/]");
        table.AddRow("[grey]Connected clients[/]",
            $"[green]{FormatHelper.Number(b.ClientsConnected)}[/] / max {FormatHelper.Number(b.ClientsMaximum)}");
        table.AddRow("[grey]Total / Expired[/]",
            $"{FormatHelper.Number(b.ClientsTotal)} / [grey]{FormatHelper.Number(b.ClientsExpired)}[/]");
        table.AddRow("[grey]Subscriptions[/]", $"[white]{FormatHelper.Number(b.SubscriptionsCount)}[/]");
        table.AddRow("[grey]Retained messages[/]", $"[white]{FormatHelper.Number(b.RetainedMessages)}[/]");
        table.AddRow(new Markup("[grey]──── Messages ────[/]"), new Markup(""));
        table.AddRow("[grey]Received / Sent[/]",
            $"{FormatHelper.Number(b.MessagesReceived)} / {FormatHelper.Number(b.MessagesSent)}");
        table.AddRow("[grey]Dropped[/]",
            $"[{ColorHelper.Dropped(b.MessagesDropped)}]{FormatHelper.Number(b.MessagesDropped)}[/]");
        table.AddRow("[grey]Inflight[/]",
            $"[{ColorHelper.Inflight(b.MessagesInflight, alerts.InflightMessagesThreshold)}]{FormatHelper.Number(b.MessagesInflight)}[/]");
        table.AddRow("[grey]Stored (offline queue)[/]",
            $"[{ColorHelper.Stored(b.MessagesStored, alerts.StoredMessagesThreshold)}]{FormatHelper.Number(b.MessagesStored)}[/]");
        table.AddRow("[grey]Bytes received / sent[/]",
            $"{FormatHelper.Bytes(b.BytesReceived)} / {FormatHelper.Bytes(b.BytesSent)}");

        return new Panel(table)
            .Header("[bold]Broker[/]", Justify.Left)
            .Border(BoxBorder.Rounded)
            .Expand();
    }
}
