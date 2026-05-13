using Spectre.Console;
using Spectre.Console.Rendering;

namespace MQTop.Tui.Panels;

public static class FooterPanel
{
    public static IRenderable Render()
    {
        var text = "[grey]Shortcuts:[/] " +
                   "[white]Q[/]/[white]Ctrl+C[/] quit  " +
                   "[white]L[/] log panel  " +
                   "[white]V[/] vendor filter  " +
                   "[white]R[/] reset counters  " +
                   "[white]↑/↓[/] scroll terminals";
        return new Panel(new Markup(text))
            .Border(BoxBorder.None)
            .Padding(0, 0, 0, 0);
    }
}
