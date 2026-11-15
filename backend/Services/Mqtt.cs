using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Dapper;
using Iot04.Api.Infrastructure;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace Iot04.Api.Services;

/// <summary>
/// MQTT broker nhúng (thay Mosquitto khi chạy không có Docker). Cảm biến thật có thể publish tới
/// tcp://&lt;host&gt;:1883, topic: warehouse/{wId}/zone/{zId}/sensor/{hardwareId}/reading
/// Payload: {"ts":"2026-10-24T09:00:00Z","temperature":4.5,"humidity":52.1,"battery":90,"rssi":-65}
/// </summary>
public sealed class MqttBrokerService(IConfiguration cfg, IngestionService ingestion, ILogger<MqttBrokerService> log) : IHostedService
{
    private MqttServer? _server;
    public bool Running => _server?.IsStarted ?? false;
    public int Port => cfg.GetValue("Mqtt:Port", 1883);

    public async Task StartAsync(CancellationToken ct)
    {
        if (!cfg.GetValue("Mqtt:Enabled", true)) return;
        var options = new MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointPort(Port).Build();
        _server = new MqttFactory().CreateMqttServer(options);
        _server.InterceptingPublishAsync += OnPublishAsync;
        _server.ClientConnectedAsync += e => { log.LogInformation("MQTT client kết nối: {Client}", e.ClientId); return Task.CompletedTask; };
        _server.ClientDisconnectedAsync += e => { log.LogInformation("MQTT client ngắt: {Client} ({Type})", e.ClientId, e.DisconnectType); return Task.CompletedTask; };
        try
        {
            await _server.StartAsync();
            log.LogInformation("MQTT broker đang lắng nghe tại cổng {Port}", Port);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Không khởi động được MQTT broker tại cổng {Port} (cổng bận?). Có thể dùng POST /api/ingest thay thế.", Port);
            _server = null;
        }
    }

    private async Task OnPublishAsync(InterceptingPublishEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var parts = topic.Split('/');
        if (parts.Length != 7 || parts[0] != "warehouse" || parts[2] != "zone" || parts[4] != "sensor") return;

        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
        switch (parts[6])
        {
            case "reading":
                await ingestion.ProcessAsync(parts[5], payload, "MQTT");
                break;
            case "status": // Last Will & Testament (BR07): chỉ ghi log — FSM vẫn dựa vào dataAge để tránh báo động giả
                log.LogWarning("LWT từ {Hw}: {Payload}", parts[5], payload);
                break;
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_server is not null) await _server.StopAsync();
    }
}

/// <summary>Trạng thái điều khiển của bộ giả lập — cho phép demo các kịch bản sự cố từ giao diện.</summary>
public sealed class SimulatorState
{
    public sealed class SensorSim
    {
        public string HardwareId { get; init; } = "";
        public bool ForcedOffline { get; set; }
        public double DriftPerHour { get; set; }
        public double DriftOffset { get; set; }
        public double Battery { get; set; } = 100;
        public bool Excursion { get; set; }
        public DateTime? LastSentAt { get; set; }
    }

    public ConcurrentDictionary<string, SensorSim> Sensors { get; } = new();
    public bool Enabled { get; set; }

    public void ResetDrift(string hardwareId)
    {
        if (Sensors.TryGetValue(hardwareId, out var s)) { s.DriftOffset = 0; s.DriftPerHour = 0; }
    }
}

/// <summary>
/// Bộ giả lập cảm biến (tương đương container sensor-simulator): publish MQTT theo đúng expected_interval của từng cảm biến.
/// Kịch bản tích hợp sẵn: SN-COLD-02-B trôi +0.15°C/h, SN-COOL-02-B pin yếu,
/// SN-DOCK-01-A vượt ngưỡng 10 phút mỗi giờ, SN-DRY-01-A gửi giá trị phi vật lý mỗi 30 phút.
/// </summary>
public sealed class SensorSimulator(Db db, SimulatorState state, MqttBrokerService broker, IConfiguration cfg, ILogger<SensorSimulator> log) : BackgroundService
{
    private sealed class Target
    {
        public string HardwareId { get; set; } = "";
        public Guid WarehouseId { get; set; }
        public Guid ZoneId { get; set; }
        public int ExpectedIntervalSec { get; set; }
        public int? BatteryLevel { get; set; }
        public DateTime? LastReadingAt { get; set; }
        public decimal TempMin { get; set; }
        public decimal TempMax { get; set; }
        public decimal HumidityMin { get; set; }
        public decimal HumidityMax { get; set; }
    }

    private readonly Random _rng = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        state.Enabled = cfg.GetValue("Simulator:Enabled", true);
        if (!state.Enabled) return;
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        if (!broker.Running) { log.LogWarning("MQTT broker không chạy — tắt bộ giả lập"); state.Enabled = false; return; }

        var client = new MqttFactory().CreateMqttClient();
        var options = new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", broker.Port).WithClientId("iot04-sensor-simulator").Build();

        List<Target> targets = [];
        var nextDue = new Dictionary<string, DateTime>();
        var lastReload = DateTime.MinValue;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected) await client.ConnectAsync(options, ct);

                if (DateTime.UtcNow - lastReload > TimeSpan.FromSeconds(30))
                {
                    await using var c = await db.OpenAsync();
                    targets = (await c.QueryAsync<Target>(@"
                        SELECT s.hardware_id AS HardwareId, z.warehouse_id AS WarehouseId, s.zone_id AS ZoneId, s.expected_interval_sec AS ExpectedIntervalSec,
                               s.battery_level AS BatteryLevel, s.last_reading_at AS LastReadingAt,
                               z.temp_min AS TempMin, z.temp_max AS TempMax, z.humidity_min AS HumidityMin, z.humidity_max AS HumidityMax
                        FROM sensors s JOIN zones z ON z.id = s.zone_id WHERE s.status <> 'DECOMMISSIONED'")).ToList();
                    foreach (var t in targets)
                    {
                        var sim = state.Sensors.GetOrAdd(t.HardwareId, hw => new SimulatorState.SensorSim
                        {
                            HardwareId = hw,
                            Battery = t.BatteryLevel ?? 100,
                            DriftPerHour = hw == "SN-COLD-02-B" ? 0.15 : 0,
                            DriftOffset = hw == "SN-COLD-02-B" ? 0.9 : 0,
                        });
                        if (!nextDue.ContainsKey(t.HardwareId))
                        {
                            // Tiếp nối dòng thời gian thật; nếu đã quá hạn thì gửi rải rác trong 20s đầu
                            var due = (t.LastReadingAt ?? DateTime.UtcNow).AddSeconds(t.ExpectedIntervalSec);
                            nextDue[t.HardwareId] = due < DateTime.UtcNow ? DateTime.UtcNow.AddSeconds(_rng.Next(1, 20)) : due;
                        }
                    }
                    lastReload = DateTime.UtcNow;
                }

                var now = DateTime.UtcNow;
                foreach (var t in targets.Where(t => nextDue.TryGetValue(t.HardwareId, out var d) && d <= now))
                {
                    nextDue[t.HardwareId] = now.AddSeconds(t.ExpectedIntervalSec);
                    var sim = state.Sensors[t.HardwareId];
                    if (sim.ForcedOffline) continue;   // mô phỏng mất kết nối: không gửi gì cả

                    var json = JsonSerializer.Serialize(BuildPayload(t, sim, now));
                    var msg = new MqttApplicationMessageBuilder()
                        .WithTopic($"warehouse/{t.WarehouseId}/zone/{t.ZoneId}/sensor/{t.HardwareId}/reading")
                        .WithPayload(json)
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build();
                    await client.PublishAsync(msg, ct);
                    sim.LastSentAt = now;
                }
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "Simulator lỗi");
                await Task.Delay(5000, ct);
            }
        }
    }

    private object BuildPayload(Target t, SimulatorState.SensorSim sim, DateTime now)
    {
        var tMid = (double)(t.TempMin + t.TempMax) / 2;
        var tRange = (double)(t.TempMax - t.TempMin);
        var hMid = (double)(t.HumidityMin + t.HumidityMax) / 2;
        var hRange = (double)(t.HumidityMax - t.HumidityMin);
        var phase = now.TimeOfDay.TotalHours / 24 * 2 * Math.PI;   // dao động ngày — chung cho cả zone

        if (sim.DriftPerHour != 0 && sim.LastSentAt is { } last)
            sim.DriftOffset = Math.Min(3.0, sim.DriftOffset + sim.DriftPerHour * (now - last).TotalHours);

        double temp = tMid + 0.2 * tRange * Math.Sin(phase) + (_rng.NextDouble() - 0.5) * 0.3 + sim.DriftOffset;
        double hum = hMid + 0.15 * hRange * Math.Sin(phase + 1) + (_rng.NextDouble() - 0.5) * 1.0;

        var excursion = sim.Excursion || (t.HardwareId == "SN-DOCK-01-A" && now.Minute is >= 20 and < 30);
        if (excursion) temp = (double)t.TempMax + 1.0;
        if (t.HardwareId == "SN-DRY-01-A" && now.Minute is 0 or 30 && now.Second < 40 && _rng.NextDouble() < 0.5) temp = 250;  // lỗi phần cứng

        sim.Battery = Math.Max(0, sim.Battery - 0.002);
        return new
        {
            ts = now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            temperature = Math.Round(temp, 2),
            humidity = Math.Round(Math.Clamp(hum, 0, 100), 2),
            battery = (int)Math.Round(sim.Battery),
            rssi = -60 - _rng.Next(0, 15)
        };
    }
}
