using System.Collections.Concurrent;
using MQTop.Configuration;
using MQTop.Models;

namespace MQTop.Services;

public class DashboardState
{
    private readonly DashboardOptions _options;
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new();
    private readonly object _logLock = new();
    private readonly LinkedList<ConnectionLogEntry> _connectionLog = new();
    private const int MaxLogEntries = 10;

    public BrokerStats Broker { get; } = new();
    public ConcurrentDictionary<string, VendorStats> Vendors { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public DateTime StartedAt { get; } = DateTime.UtcNow;

    public DashboardState(DashboardOptions options)
    {
        _options = options;
        foreach (var v in options.Vendors)
        {
            Vendors[v] = new VendorStats { Name = v };
        }
    }

    private VendorStats GetOrAddVendor(string vendor) =>
        Vendors.GetOrAdd(vendor, n => new VendorStats { Name = n });

    public void UpdateBrokerStat(string topic, string value)
    {
        try
        {
            switch (topic)
            {
                case "$SYS/broker/version":
                    Broker.Version = value;
                    break;
                case "$SYS/broker/uptime":
                    Broker.Uptime = value;
                    break;
                case "$SYS/broker/clients/connected":
                    Broker.ClientsConnected = ParseInt(value);
                    break;
                case "$SYS/broker/clients/disconnected":
                    Broker.ClientsDisconnected = ParseInt(value);
                    break;
                case "$SYS/broker/clients/maximum":
                    Broker.ClientsMaximum = ParseInt(value);
                    break;
                case "$SYS/broker/clients/total":
                    Broker.ClientsTotal = ParseInt(value);
                    break;
                case "$SYS/broker/clients/expired":
                    Broker.ClientsExpired = ParseInt(value);
                    break;
                case "$SYS/broker/messages/received":
                    Broker.MessagesReceived = ParseLong(value);
                    break;
                case "$SYS/broker/messages/sent":
                    Broker.MessagesSent = ParseLong(value);
                    break;
                case "$SYS/broker/messages/dropped":
                    Broker.MessagesDropped = ParseLong(value);
                    break;
                case "$SYS/broker/messages/inflight":
                    Broker.MessagesInflight = ParseInt(value);
                    break;
                case "$SYS/broker/messages/stored":
                    Broker.MessagesStored = ParseInt(value);
                    break;
                case "$SYS/broker/retained messages/count":
                    Broker.RetainedMessages = ParseInt(value);
                    break;
                case "$SYS/broker/publish/messages/received":
                    Broker.PublishMessagesReceived = ParseLong(value);
                    break;
                case "$SYS/broker/publish/messages/sent":
                    Broker.PublishMessagesSent = ParseLong(value);
                    break;
                case "$SYS/broker/publish/messages/dropped":
                    Broker.PublishMessagesDropped = ParseLong(value);
                    break;
                case "$SYS/broker/bytes/received":
                    Broker.BytesReceived = ParseLong(value);
                    break;
                case "$SYS/broker/bytes/sent":
                    Broker.BytesSent = ParseLong(value);
                    break;
                case "$SYS/broker/load/connections/1min":
                    Broker.LoadConnections1Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/connections/5min":
                    Broker.LoadConnections5Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/connections/15min":
                    Broker.LoadConnections15Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/messages/received/1min":
                    Broker.LoadMsgReceived1Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/messages/received/5min":
                    Broker.LoadMsgReceived5Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/messages/received/15min":
                    Broker.LoadMsgReceived15Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/messages/sent/1min":
                    Broker.LoadMsgSent1Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/messages/sent/5min":
                    Broker.LoadMsgSent5Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/messages/sent/15min":
                    Broker.LoadMsgSent15Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/publish/dropped/1min":
                    Broker.LoadMsgDropped1Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/bytes/received/1min":
                    Broker.LoadBytesReceived1Min = ParseDouble(value);
                    break;
                case "$SYS/broker/load/bytes/sent/1min":
                    Broker.LoadBytesSent1Min = ParseDouble(value);
                    break;
                case "$SYS/broker/subscriptions/count":
                    Broker.SubscriptionsCount = ParseInt(value);
                    break;
            }
        }
        catch
        {
            // Malformed value - ignore
        }
    }

    public void UpdateTerminalStatus(string vendor, string serialNo, string status, DateTime timestamp)
    {
        var vs = GetOrAddVendor(vendor);
        var terminal = vs.Terminals.GetOrAdd(serialNo, sn => new TerminalInfo
        {
            SerialNo = sn,
            Vendor = vendor,
            ConnectedAt = timestamp
        });

        var wasOnline = string.Equals(terminal.Status, "online", StringComparison.OrdinalIgnoreCase);
        var willBeOnline = string.Equals(status, "online", StringComparison.OrdinalIgnoreCase);

        terminal.Status = status;
        terminal.LastSeen = timestamp;
        if (!wasOnline && willBeOnline)
        {
            terminal.ConnectedAt = timestamp;
            AddConnectionLog($"{serialNo} came online");
        }
        else if (wasOnline && !willBeOnline)
        {
            AddConnectionLog($"{serialNo} went offline");
        }
    }

    public void IncrementCmd(string vendor, string serialNo, string requestId, string type)
    {
        var vs = GetOrAddVendor(vendor);
        var terminal = vs.Terminals.GetOrAdd(serialNo, sn => new TerminalInfo
        {
            SerialNo = sn,
            Vendor = vendor,
            ConnectedAt = DateTime.UtcNow
        });

        Interlocked.Increment(ref terminal.CmdCount);
        Interlocked.Increment(ref vs.TotalCmdCount);
        terminal.LastCommandType = type;
        terminal.LastCommandAt = DateTime.UtcNow;
        terminal.LastSeen = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(requestId))
        {
            _pending[requestId] = new PendingRequest(vendor, serialNo, DateTime.UtcNow);
        }
    }

    public void IncrementResponse(string vendor, string serialNo, string requestId, bool success)
    {
        var vs = GetOrAddVendor(vendor);
        var terminal = vs.Terminals.GetOrAdd(serialNo, sn => new TerminalInfo
        {
            SerialNo = sn,
            Vendor = vendor,
            ConnectedAt = DateTime.UtcNow
        });

        Interlocked.Increment(ref terminal.ResponseCount);
        Interlocked.Increment(ref vs.TotalResponseCount);
        terminal.LastSeen = DateTime.UtcNow;

        if (!success)
        {
            Interlocked.Increment(ref terminal.DroppedCount);
            Interlocked.Increment(ref vs.TotalDroppedCount);
        }

        if (!string.IsNullOrEmpty(requestId))
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    public void SweepPendingTimeouts(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _pending)
        {
            if (now - kvp.Value.SentAt <= timeout) continue;
            if (!_pending.TryRemove(kvp.Key, out var pending)) continue;
            if (!Vendors.TryGetValue(pending.Vendor, out var vs)) continue;
            if (!vs.Terminals.TryGetValue(pending.SerialNo, out var terminal)) continue;
            Interlocked.Increment(ref terminal.DroppedCount);
            Interlocked.Increment(ref vs.TotalDroppedCount);
        }
    }

    public IEnumerable<TerminalInfo> GetAllTerminals()
    {
        var all = Vendors.Values.SelectMany(v => v.Terminals.Values).ToList();
        return all
            .OrderByDescending(t => string.Equals(t.Status, "online", StringComparison.OrdinalIgnoreCase))
            .ThenBy(t => t.Vendor, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.SerialNo, StringComparer.OrdinalIgnoreCase);
    }

    public void ResetCounters()
    {
        foreach (var vs in Vendors.Values)
        {
            Interlocked.Exchange(ref vs.TotalCmdCount, 0);
            Interlocked.Exchange(ref vs.TotalResponseCount, 0);
            Interlocked.Exchange(ref vs.TotalDroppedCount, 0);
            foreach (var t in vs.Terminals.Values)
            {
                Interlocked.Exchange(ref t.CmdCount, 0);
                Interlocked.Exchange(ref t.ResponseCount, 0);
                Interlocked.Exchange(ref t.DroppedCount, 0);
            }
        }
        _pending.Clear();
        AddConnectionLog("Counters reset");
    }

    public void SetBrokerConnected(bool connected, string? reason = null)
    {
        Broker.IsConnected = connected;
        if (connected)
        {
            Broker.LastConnectedAt = DateTime.UtcNow;
            AddConnectionLog("Connected to broker");
        }
        else
        {
            Broker.LastDisconnectedAt = DateTime.UtcNow;
            AddConnectionLog(string.IsNullOrEmpty(reason)
                ? "Disconnected from broker"
                : $"Disconnected from broker (reason: {reason})");
        }
    }

    public void RecordReconnectAttempt(int attempt)
    {
        Broker.LastReconnectAttemptAt = DateTime.UtcNow;
        Broker.ReconnectAttempts = attempt;
        AddConnectionLog($"Reconnecting... attempt {attempt}");
    }

    public void AddConnectionLog(string message)
    {
        lock (_logLock)
        {
            _connectionLog.AddLast(new ConnectionLogEntry
            {
                Timestamp = DateTime.Now,
                Message = message
            });
            while (_connectionLog.Count > MaxLogEntries)
            {
                _connectionLog.RemoveFirst();
            }
        }
    }

    public IReadOnlyList<ConnectionLogEntry> GetConnectionLog()
    {
        lock (_logLock)
        {
            return _connectionLog.ToList();
        }
    }

    private static int ParseInt(string s) =>
        int.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static long ParseLong(string s) =>
        long.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0L;

    private static double ParseDouble(string s) =>
        double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0.0;

    private record PendingRequest(string Vendor, string SerialNo, DateTime SentAt);
}
