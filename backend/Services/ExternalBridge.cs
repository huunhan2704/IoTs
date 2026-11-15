using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;

namespace Iot04.Api.Services;

// External MQTT Bridge: Cho phép ESP32 từ Wokwi hoặc phần cứng thật ở bất kỳ đâu
// gửi về broker.hivemq.com và backend tự động kéo về xử lý!
public sealed class ExternalMqttBridgeService(IConfiguration cfg, IngestionService ingestion, ILogger<ExternalMqttBridgeService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!cfg.GetValue("Mqtt:BridgeEnabled", true)) return;
        var host = cfg.GetValue("Mqtt:ExternalBroker", "broker.hivemq.com");
        var port = cfg.GetValue("Mqtt:ExternalPort", 1883);

        await Task.Delay(2000, ct);

        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId($"iot04-bridge-{Guid.NewGuid():N}")
            .WithCleanSession()
            .Build();

        client.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var parts = topic.Split('/');
            if (parts.Length != 7 || parts[0] != "warehouse" || parts[2] != "zone" || parts[4] != "sensor") return;

            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            if (parts[6] == "reading")
            {
                await ingestion.ProcessAsync(parts[5], payload, "WOKWI_HIVEMQ");
            }
        };

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    log.LogInformation("Cầu nối Wokwi đang kết nối MQTT Public Broker: {Host}:{Port}...", host, port);
                    await client.ConnectAsync(options, ct);
                    await client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("warehouse/#").Build(), ct);
                    log.LogInformation("Cầu nối Wokwi đã kích hoạt! Sẵn sàng nhận dữ liệu từ mọi ESP32 Wokwi.");
                }
                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogWarning("Mất kết nối cầu nối HiveMQ ({Msg}), thử lại sau 5s...", ex.Message);
                await Task.Delay(5000, ct);
            }
        }
    }
}
