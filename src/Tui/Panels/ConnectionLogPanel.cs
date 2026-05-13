using Spectre.Console;
using Spectre.Console.Rendering;
using MQTop.Services;

namespace MQTop.Tui.Panels;

public static class ConnectionLogPanel
{
    public static IRenderable Render(DashboardState state)
    {
        var entries = state.GetConnectionLog();
        if (entries.Count == 0)
        {
            return new Panel(new Markup("[grey italic]No log entries yet[/]"))
                .Header("[bold]Connection log[/]")
                .Border(BoxBorder.Rounded);
        }

        var rows = entries.Select(e =>
            (IRenderable)new Markup(
                $"[grey][[{e.Timestamp:HH:mm:ss}]][/] {Markup.Escape(e.Message)}"))
            .ToArray();

        return new Panel(new Rows(rows))
            .Header($"[bold]Connection log ({entries.Count})[/]")
            .Border(BoxBorder.Rounded);
    }
}
