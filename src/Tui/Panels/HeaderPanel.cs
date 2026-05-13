using Spectre.Console;
using Spectre.Console.Rendering;
using MQTop.Services;
using MQTop.Tui.Helpers;

namespace MQTop.Tui.Panels;

public static class HeaderPanel
{
    public static IRenderable Render(DashboardState state, string vendorFilter)
    {
        var broker = state.Broker;
        var statusText = broker.IsConnected
            ? "[green bold]● CONNECTED[/]"
            : "[red bold]○ DISCONNECTED — RECONNECTING...[/]";

        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var reconnectInfo = !broker.IsConnected && broker.LastReconnectAttemptAt.HasValue
            ? $"  [yellow]Last attempt: {FormatHelper.DurationSince(broker.LastReconnectAttemptAt.Value.ToUniversalTime())} ago (#{broker.ReconnectAttempts})[/]"
            : string.Empty;

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap())
            .AddColumn(new GridColumn().NoWrap().RightAligned());

        grid.AddRow(
            new Markup($"[bold cyan]MQTop[/]  [grey]Mosquitto Monitor[/]  {statusText}{reconnectInfo}"),
            new Markup($"[grey]Filter:[/] [white]{Markup.Escape(vendorFilter)}[/]  [grey]{now}[/]"));

        return new Panel(grid)
            .Border(BoxBorder.Rounded)
            .BorderColor(broker.IsConnected ? Color.Green : Color.Red)
            .Expand();
    }
}
