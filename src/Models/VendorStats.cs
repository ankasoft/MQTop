using System.Collections.Concurrent;

namespace MQTop.Models;

public class VendorStats
{
    public string Name { get; set; } = string.Empty;
    public long TotalCmdCount;
    public long TotalResponseCount;
    public long TotalDroppedCount;

    public ConcurrentDictionary<string, TerminalInfo> Terminals { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public int OnlineCount =>
        Terminals.Values.Count(t => string.Equals(t.Status, "online", StringComparison.OrdinalIgnoreCase));

    public int OfflineCount =>
        Terminals.Values.Count(t => !string.Equals(t.Status, "online", StringComparison.OrdinalIgnoreCase));

    public int TotalTerminals => Terminals.Count;

    public double SuccessRate
    {
        get
        {
            var cmd = Interlocked.Read(ref TotalCmdCount);
            if (cmd == 0) return 100.0;
            var resp = Interlocked.Read(ref TotalResponseCount);
            return Math.Min(100.0, (double)resp / cmd * 100.0);
        }
    }
}
