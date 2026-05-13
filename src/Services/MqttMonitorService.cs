using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTop.Configuration;

namespace MQTop.Services;

public class MqttMonitorService : BackgroundService
{
    private static readonly string[] SysTopics =
    {
        "$SYS/broker/version",
        "$SYS/broker/uptime",
        "$SYS/broker/clients/connected",
        "$SYS/broker/clients/disconnected",
        "$SYS/broker/clients/maximum",
        "$SYS/broker/clients/total",
        "$SYS/broker/clients/expired",
        "$SYS/broker/messages/received",
        "$SYS/broker/messages/sent",
        "$SYS/broker/messages/dropped",
        "$SYS/broker/messages/inflight",
        "$SYS/broker/messages/stored",
        "$SYS/broker/retained messages/count",
        "$SYS/broker/publish/messages/received",
        "$SYS/broker/publish/messages/sent",
        "$SYS/broker/publish/messages/dropped",
        "$SYS/broker/bytes/received",
        "$SYS/broker/bytes/sent",
        "$SYS/broker/load/connections/1min",
        "$SYS/broker/load/connections/5min",
        "$SYS/broker/load/connections/15min",
        "$SYS/broker/load/messages/received/1min",
        "$SYS/broker/load/messages/received/5min",
        "$SYS/broker/load/messages/received/15min",
        "$SYS/broker/load/messages/sent/1min",
        "$SYS/broker/load/messages/sent/5min",
        "$SYS/broker/load/messages/sent/15min",
        "$SYS/broker/load/publish/dropped/1min",
        "$SYS/broker/load/bytes/received/1min",
        "$SYS/broker/load/bytes/sent/1min",
        "$SYS/broker/subscriptions/count"
    };

    private static readonly TimeSpan[] BackoffSchedule =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
        TimeSpan.FromSeconds(30)
    };

    private readonly DashboardState _state;
    private readonly MqttOptions _mqttOptions;
    private readonly DashboardOptions _dashboardOptions;
    private readonly ILogger<MqttMonitorService> _logger;
    private IMqttClient? _client;

    public MqttMonitorService(
        DashboardState state,
        IOptions<MqttOptions> mqttOptions,
        IOptions<DashboardOptions> dashboardOptions,
        ILogger<MqttMonitorService> logger)
    {
        _state = state;
        _mqttOptions = mqttOptions.Value;
        _dashboardOptions = dashboardOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        _client.DisconnectedAsync += async args =>
        {
            _state.SetBrokerConnected(false, args.Reason.ToString());
            await Task.CompletedTask;
        };
        _client.ConnectedAsync += async _ =>
        {
            _state.SetBrokerConnected(true);
            await SubscribeAllAsync();
        };

        _ = Task.Run(() => SweepTimeoutsAsync(stoppingToken), stoppingToken);

        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    if (attempt > 0)
                    {
                        _state.RecordReconnectAttempt(attempt);
                    }
                    var options = BuildClientOptions();
                    await _client.ConnectAsync(options, stoppingToken);
                    attempt = 0;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                attempt++;
                var delay = BackoffSchedule[Math.Min(attempt - 1, BackoffSchedule.Length - 1)];
                _logger.LogWarning(ex, "MQTT connection failed, retrying in {Delay}s", delay.TotalSeconds);
                _state.AddConnectionLog($"Connect failed: {ex.Message}");
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }
        catch { /* ignore */ }
    }

    private MqttClientOptions BuildClientOptions()
    {
        var uri = new Uri(_mqttOptions.BrokerUrl);
        var builder = new MqttClientOptionsBuilder()
            .WithClientId($"{_mqttOptions.ClientId}-{Guid.NewGuid():N}".Substring(0, 23))
            .WithTcpServer(uri.Host, uri.Port > 0 ? uri.Port : 1883)
            .WithCleanSession(true)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));

        if (!string.IsNullOrEmpty(_mqttOptions.Username))
        {
            builder = builder.WithCredentials(_mqttOptions.Username, _mqttOptions.Password);
        }

        if (string.Equals(uri.Scheme, "mqtts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, "ssl", StringComparison.OrdinalIgnoreCase))
        {
            builder = builder.WithTlsOptions(o => o.UseTls());
        }

        return builder.Build();
    }

    private async Task SubscribeAllAsync()
    {
        if (_client == null) return;

        var subBuilder = new MqttClientSubscribeOptionsBuilder();
        foreach (var topic in SysTopics)
        {
            subBuilder = subBuilder.WithTopicFilter(topic, MqttQualityOfServiceLevel.AtLeastOnce);
        }
        // Terminal topics — wildcard subscribe; we filter by vendor list at message time.
        subBuilder = subBuilder
            .WithTopicFilter("+/+/status", MqttQualityOfServiceLevel.AtLeastOnce)
            .WithTopicFilter("+/+/cmd", MqttQualityOfServiceLevel.AtLeastOnce)
            .WithTopicFilter("+/+/response", MqttQualityOfServiceLevel.AtLeastOnce);

        try
        {
            await _client.SubscribeAsync(subBuilder.Build());
            _state.AddConnectionLog("Subscribed to all topics");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscribe failed");
            _state.AddConnectionLog($"Subscribe failed: {ex.Message}");
        }
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        // Fire-and-forget; never block the client.
        _ = Task.Run(() =>
        {
            try
            {
                var topic = args.ApplicationMessage.Topic;
                var payload = args.ApplicationMessage.PayloadSegment.Count > 0
                    ? Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment)
                    : string.Empty;

                if (topic.StartsWith("$SYS/", StringComparison.Ordinal))
                {
                    _state.UpdateBrokerStat(topic, payload);
                    return;
                }

                var parts = topic.Split('/');
                if (parts.Length != 3) return;

                var vendor = parts[0];
                var serialNo = parts[1];
                var kind = parts[2];

                // Vendor filter — only known vendors from config when list is non-empty.
                if (_dashboardOptions.Vendors.Count > 0 &&
                    !_dashboardOptions.Vendors.Any(v =>
                        string.Equals(v, vendor, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                switch (kind)
                {
                    case "status":
                        HandleStatus(vendor, serialNo, payload);
                        break;
                    case "cmd":
                        HandleCmd(vendor, serialNo, payload);
                        break;
                    case "response":
                        HandleResponse(vendor, serialNo, payload);
                        break;
                }
            }
            catch (Exception ex)
            {
                _state.AddConnectionLog($"Message parse error: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }

    private void HandleStatus(string vendor, string serialNo, string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "offline" : "offline";
            var ts = root.TryGetProperty("timestamp", out var t) && t.ValueKind == JsonValueKind.String
                && DateTime.TryParse(t.GetString(), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow;
            _state.UpdateTerminalStatus(vendor, serialNo, status, ts);
        }
        catch
        {
            // malformed — treat presence as online.
            _state.UpdateTerminalStatus(vendor, serialNo, "online", DateTime.UtcNow);
        }
    }

    private void HandleCmd(string vendor, string serialNo, string payload)
    {
        string requestId = string.Empty;
        string type = "—";
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("requestId", out var r)) requestId = r.GetString() ?? string.Empty;
            if (root.TryGetProperty("type", out var ty)) type = ty.GetString() ?? "—";
        }
        catch { /* malformed payload — still increment counter */ }
        _state.IncrementCmd(vendor, serialNo, requestId, type);
    }

    private void HandleResponse(string vendor, string serialNo, string payload)
    {
        string requestId = string.Empty;
        bool success = true;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("requestId", out var r)) requestId = r.GetString() ?? string.Empty;
            if (root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.False) success = false;
        }
        catch { /* malformed — still count */ }
        _state.IncrementResponse(vendor, serialNo, requestId, success);
    }

    private async Task SweepTimeoutsAsync(CancellationToken token)
    {
        var timeout = TimeSpan.FromSeconds(_dashboardOptions.TerminalTimeoutSeconds);
        while (!token.IsCancellationRequested)
        {
            try
            {
                _state.SweepPendingTimeouts(timeout);
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore */ }
        }
    }
}
