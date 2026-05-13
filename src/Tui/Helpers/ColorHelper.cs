namespace MQTop.Tui.Helpers;

public static class ColorHelper
{
    public static string SuccessRate(double rate)
    {
        if (rate >= 99.0) return "green";
        if (rate >= 95.0) return "yellow";
        return "red";
    }

    public static string Dropped(long count) => count > 0 ? "red" : "green";

    public static string Inflight(int count, int threshold) =>
        count > threshold ? "yellow" : "green";

    public static string Stored(int count, int threshold) =>
        count > threshold ? "yellow" : "green";

    public static string LoadDropped(double v) => v > 0 ? "red" : "green";

    public static string LastSeen(DateTime utc, string status)
    {
        var online = string.Equals(status, "online", StringComparison.OrdinalIgnoreCase);
        if (!online) return "grey";
        var diff = DateTime.UtcNow - utc;
        if (diff.TotalSeconds < 30) return "green";
        if (diff.TotalMinutes < 5) return "yellow";
        return "red";
    }

    public static string StatusDot(string status) =>
        string.Equals(status, "online", StringComparison.OrdinalIgnoreCase)
            ? "[green]●[/]"
            : "[red]○[/]";
}
