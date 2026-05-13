# MQTop

**A real-time TUI dashboard for monitoring a Mosquitto MQTT broker and the terminals connected to it.**

No browser, no Grafana stack, no extra service — broker health and the live status of every connected terminal in a single terminal window.

Built with .NET 10, [Spectre.Console](https://spectreconsole.net), and [MQTTnet](https://github.com/dotnet/MQTTnet). Single binary, single config file, MIT licensed.

```
┌─ Broker ────────────────┐ ┌─ Load ───────────────────────┐
│ Version mosquitto 2.0.18│ │ Msg received/s   17.20       │
│ Uptime  2d 4h 17m       │ │ Dropped/s         0.00       │
│ Clients   42 / max 87   │ │ Bytes received/s 12.4 KB/s   │
│ Dropped    0            │ │                              │
└─────────────────────────┘ └──────────────────────────────┘
```

---

## Why?

Mosquitto's `$SYS/broker/*` topics expose a lot about broker health. But day to day:

- `mosquitto_sub` means walking topic-by-topic manually.
- A full Prometheus + Grafana stack is overkill for most setups.
- The things that matter most — broker state **and** your own `cmd`/`response` traffic — are never on the same screen.

MQTop merges the two: broker `$SYS` metrics and per-terminal command/response counters, live, in one threshold-coloured dashboard.

---

## What it shows

| Panel | Contents |
|---|---|
| **Header** | Broker connection state, clock, active vendor filter, last reconnect attempt |
| **Broker** | Version, uptime, connected/max/total clients, message counters (received/sent/dropped/inflight/stored), retained messages, subscription count |
| **Load** | 1 / 5 / 15-minute averages — connections, msg/s, bytes/s, dropped/s |
| **Vendors** | Per-vendor online/offline terminal counts, total cmd/resp/drop, success rate |
| **Terminals** | Scrollable list — serial no, vendor, online/offline dot, cmd/resp/drop counts, success %, last command type, last-seen duration |
| **Alerts** | Auto-generated, colour-coded alert lines on threshold breach (rotates if more than 3 are active) |
| **Connection log** *(toggle with `L`)* | Last 10 connect/disconnect/subscribe events |

### Colour coding

| State | Colour |
|---|---|
| Connected, success rate ≥ 99%, last-seen < 30s | green |
| Inflight/stored threshold exceeded, success rate 95% – 99%, last-seen 30s – 5m | yellow |
| Broker disconnect, dropped > 0, success rate < 95%, last-seen > 5m | red |

Thresholds are tunable in `appsettings.json`.

---

## How it works

```
        ┌─────────────────────────────────────────────┐
        │              Mosquitto Broker               │
        │   $SYS/broker/*    vendor/serial/{...}      │
        └────────────────────────┬────────────────────┘
                                 │ MQTT subscribe (QoS 1)
                                 ▼
        ┌─────────────────────────────────────────────┐
        │           MqttMonitorService                │
        │   (BackgroundService)                       │
        │   • parses $SYS payloads                    │
        │   • increments cmd / response counters      │
        │   • runs the requestId timeout sweep        │
        │   • reconnects with exponential backoff     │
        └────────────────────────┬────────────────────┘
                                 │ thread-safe writes
                                 ▼
        ┌─────────────────────────────────────────────┐
        │             DashboardState                  │
        │   ConcurrentDictionary + Interlocked        │
        └────────────────────────┬────────────────────┘
                                 │ read once per second
                                 ▼
        ┌─────────────────────────────────────────────┐
        │              TuiDashboard                   │
        │   (BackgroundService)                       │
        │   Spectre.Console Live render + input loop  │
        └─────────────────────────────────────────────┘
```

Two independent `BackgroundService`s share a single thread-safe state object. The MQTT message handler **never** blocks on state work — every message is processed in fire-and-forget `Task.Run`, so the broker never sees back-pressure from the monitor.

### Dropped-message detection

1. When a terminal publishes a `cmd`, its `requestId` is added to a pending list.
2. When a `response` comes back:
   - `success: true` → response counter +1
   - `success: false` → response counter +1 **and** drop counter +1
3. If no response arrives within `TerminalTimeoutSeconds` (default 300s), a sweep that runs every 5 seconds marks it as a drop.

### Auto-reconnect

When the broker connection drops:

1. State flips to `disconnected`, the header turns red and shows **RECONNECTING…**.
2. Exponential backoff: `1s → 2s → 4s → 8s → 16s → 30s` (capped).
3. On reconnect, MQTop automatically re-subscribes to every topic.
4. Each event is appended to the connection log.

---

## Installation

### From source

```bash
dotnet restore
dotnet run
```

### Standalone binary

```bash
dotnet publish -c Release -o publish
./publish/mqtop
```

### Docker

The multi-stage Dockerfile doesn't need the .NET SDK installed locally — everything is built inside the container.

```bash
docker build -t mqtop .
docker run -it --rm --network host mqtop
```

> **The TUI needs a TTY** — `-it` is required. To run detached: use `-d -it` together, then `docker attach mqtop` and detach again with `Ctrl+P, Ctrl+Q`.

---

## Configuration

`appsettings.json` with defaults:

```json
{
  "Mqtt": {
    "BrokerUrl": "mqtt://localhost:1883",
    "Username": "",
    "Password": "",
    "ClientId": "mosquitto-monitor",
    "SysTopicInterval": 10
  },
  "Dashboard": {
    "RefreshIntervalMs": 1000,
    "TerminalTimeoutSeconds": 300,
    "Vendors": ["worldline", "token", "inpos", "hugin"]
  },
  "Alerts": {
    "DroppedMessagesThreshold": 1,
    "InflightMessagesThreshold": 10,
    "StoredMessagesThreshold": 50,
    "SuccessRateWarningThreshold": 99.0,
    "SuccessRateCriticalThreshold": 95.0
  }
}
```

| Section | Key | Default | Description |
|---|---|---|---|
| Mqtt | `BrokerUrl` | `mqtt://localhost:1883` | Broker endpoint; `mqtts://` enables TLS |
| Mqtt | `Username` / `Password` | — | Optional authentication |
| Mqtt | `ClientId` | `mosquitto-monitor` | Client id prefix (a random suffix is appended) |
| Mqtt | `SysTopicInterval` | `10` | Informational; the real cadence is controlled by the broker |
| Dashboard | `RefreshIntervalMs` | `1000` | TUI redraw interval |
| Dashboard | `TerminalTimeoutSeconds` | `300` | Pending requestId is counted as a drop after this delay |
| Dashboard | `Vendors` | see above | Vendor prefixes to track |
| Alerts | `DroppedMessagesThreshold` | `1` | Critical alert once total dropped reaches this |
| Alerts | `InflightMessagesThreshold` | `10` | Yellow warning when exceeded |
| Alerts | `StoredMessagesThreshold` | `50` | Yellow warning when the offline queue grows past this |
| Alerts | `SuccessRateWarningThreshold` | `99.0` | Below this → yellow warning |
| Alerts | `SuccessRateCriticalThreshold` | `95.0` | Below this → red critical |

### Environment variable overrides

```bash
MQTOP_Mqtt__BrokerUrl=mqtt://10.0.0.5:1883
MQTOP_Mqtt__Username=monitor
MQTOP_Mqtt__Password=secret
MQTOP_Dashboard__TerminalTimeoutSeconds=180
```

`MQTOP_` prefix; section/key separated by a double underscore (`__`).

### Dynamic vendor list

Every entry in `Dashboard.Vendors` is tracked as a topic prefix. Adding a new vendor means **just adding it to the config** — no code change. If the list is empty, all `+/+/{kind}` traffic is tracked.

---

## Topic format

### Broker — `$SYS/broker/*`

Subscribed automatically. Publishing cadence is controlled by the broker's `sys_interval` (default 10s). Metrics that haven't arrived yet are shown as `—`.

### Terminal — `{vendor}/{serialNo}/{kind}`

`kind` ∈ `status` | `cmd` | `response`

**status** *(send as retained + Last Will and Testament)*

```json
{
  "status": "online",
  "timestamp": "2026-05-13T14:32:11Z"
}
```

If terminals register an LWT with `status: "offline"`, the broker publishes it automatically when the connection drops — MQTop turns the terminal red immediately.

**cmd**

```json
{
  "requestId": "uuid-v4",
  "type": "TSM_IR_GetAdisyon",
  "timestamp": "2026-05-13T14:32:11Z",
  "payload": {}
}
```

**response**

```json
{
  "requestId": "uuid-v4",
  "type": "TSM_IR_GetAdisyon",
  "success": true,
  "data": {}
}
```

`requestId` is the cmd/response correlation key — unmatched requests become drops after the timeout.

---

## Keyboard shortcuts

| Key | Action |
|---|---|
| `Q`, `Ctrl+C` | Graceful exit |
| `L` | Toggle the connection log panel |
| `V` | Cycle vendor filter (All → vendor1 → vendor2 → …) |
| `R` | Reset all cmd/response/drop counters |
| `↑` / `↓` | Scroll the terminal list by one row |
| `PgUp` / `PgDn` | Page through the terminal list |

---

## Alert rules

Alerts are generated automatically when:

**Critical (red):**
- Broker is disconnected
- Total dropped count just increased (above threshold)
- 1-minute dropped rate > 0/s
- A vendor's success rate falls below `SuccessRateCriticalThreshold`

**Warning (yellow):**
- Inflight count > `InflightMessagesThreshold`
- Stored (offline queue) > `StoredMessagesThreshold`
- A vendor's success rate falls below `SuccessRateWarningThreshold` (but still above critical)
- A terminal has been offline for more than 1 hour

If more than 3 alerts are active at once, the alerts panel rotates through them.

---

## Screenshot

> _Screenshot goes here._

```
[placeholder for screenshot]
```

---

## Project layout

```
MQTop/
├── Program.cs                          # Generic Host bootstrap
├── appsettings.json
├── Dockerfile                          # Multi-stage build
├── src/
│   ├── Configuration/
│   │   └── AppOptions.cs               # Mqtt / Dashboard / Alerts options
│   ├── Models/
│   │   ├── BrokerStats.cs
│   │   ├── VendorStats.cs
│   │   ├── TerminalInfo.cs
│   │   ├── Alert.cs
│   │   └── ConnectionLogEntry.cs
│   ├── Services/
│   │   ├── DashboardState.cs           # The single shared, thread-safe state
│   │   └── MqttMonitorService.cs       # BackgroundService — MQTT collector
│   └── Tui/
│       ├── TuiDashboard.cs             # BackgroundService — Live render + input
│       ├── Panels/
│       │   ├── HeaderPanel.cs
│       │   ├── BrokerPanel.cs
│       │   ├── LoadPanel.cs
│       │   ├── VendorPanel.cs
│       │   ├── TerminalPanel.cs
│       │   ├── AlertsPanel.cs
│       │   ├── ConnectionLogPanel.cs
│       │   └── FooterPanel.cs
│       └── Helpers/
│           ├── ColorHelper.cs          # Threshold-based colour selection
│           └── FormatHelper.cs         # Duration (d/h/m/s), bytes, %
```

---

## Development

```bash
dotnet build
dotnet run
```

A quick local Mosquitto for testing:

```bash
docker run -it --rm -p 1883:1883 \
  -v $(pwd)/mosquitto.conf:/mosquitto/config/mosquitto.conf \
  eclipse-mosquitto
```

Example `mosquitto.conf`:

```conf
listener 1883
allow_anonymous true
sys_interval 5
```

---

## Contributing

PRs welcome. Please:

1. **Never hard-code vendor names** — always read them from config.
2. Use `FormatHelper` for duration/byte/percent formatting (compact `d/h/m/s` durations, IEC byte units).
3. State access must be thread-safe (`Interlocked`, `ConcurrentDictionary`).
4. Run `dotnet build` and `dotnet format` before submitting.

---

## License

MIT — see [LICENSE](LICENSE).
