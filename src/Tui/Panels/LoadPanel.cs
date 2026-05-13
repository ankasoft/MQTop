using Spectre.Console;
using Spectre.Console.Rendering;
using MQTop.Services;
using MQTop.Tui.Helpers;

namespace MQTop.Tui.Panels;

public static class LoadPanel
{
    public static IRenderable Render(DashboardState state)
    {
        var b = state.Broker;

        var table = new Table()
            .Border(TableBorder.None)
            .AddColumn(new TableColumn("[grey]Metric[/]").NoWrap())
            .AddColumn(new TableColumn("[grey]1m[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]5m[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]15m[/]").RightAligned());

        table.AddRow("[grey]Connections[/]",
            FormatHelper.Load(b.LoadConnections1Min),
            FormatHelper.Load(b.LoadConnections5Min),
            FormatHelper.Load(b.LoadConnections15Min));
        table.AddRow("[grey]Msg received/s[/]",
            FormatHelper.Load(b.LoadMsgReceived1Min),
            FormatHelper.Load(b.LoadMsgReceived5Min),
            FormatHelper.Load(b.LoadMsgReceived15Min));
        table.AddRow("[grey]Msg sent/s[/]",
            FormatHelper.Load(b.LoadMsgSent1Min),
            FormatHelper.Load(b.LoadMsgSent5Min),
            FormatHelper.Load(b.LoadMsgSent15Min));
        table.AddRow("[grey]Dropped/s[/]",
            $"[{ColorHelper.LoadDropped(b.LoadMsgDropped1Min)}]{FormatHelper.Load(b.LoadMsgDropped1Min)}[/]",
            "—", "—");
        table.AddRow("[grey]Bytes received/s[/]",
            FormatHelper.BytesPerSecond(b.LoadBytesReceived1Min), "—", "—");
        table.AddRow("[grey]Bytes sent/s[/]",
            FormatHelper.BytesPerSecond(b.LoadBytesSent1Min), "—", "—");

        return new Panel(table)
            .Header("[bold]Load[/]", Justify.Left)
            .Border(BoxBorder.Rounded)
            .Expand();
    }
}
