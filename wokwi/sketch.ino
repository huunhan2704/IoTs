#include <WiFi.h>
#define MQTT_MAX_PACKET_SIZE 512    // Đảm bảo không bị lỗi bộ đệm và không có warning
#include <PubSubClient.h>
#include <DHTesp.h>
#include <ArduinoJson.h>

// ============================================================
// ⚠️ CẤU HÌNH CẢM BIẾN CHO TỪNG TỦ VẮC-XIN (SN-COLD-01-A -> SN-COLD-05-E)
// Tab 1: "SN-COLD-01-A"
// Tab 2: "SN-COLD-02-B"
// Tab 3: "SN-COLD-03-C"
// Tab 4: "SN-COLD-04-D"
// Tab 5: "SN-COLD-05-E"
// ============================================================
const char* HARDWARE_ID  = "SN-COLD-01-A"; 

const char* WAREHOUSE_ID = "WH-HCM-01";
const char* ZONE_ID      = "ZONE-COLD";

// WiFi ảo trên Wokwi
const char* WIFI_SSID    = "Wokwi-GUEST";
const char* WIFI_PASS    = "";

// MQTT Public Broker (HiveMQ)
const char* MQTT_BROKER  = "broker.hivemq.com";
const int   MQTT_PORT    = 1883;

char topicReading[128];
const int DHT_PIN = 15;
DHTesp dht;
WiFiClient espClient;
PubSubClient client(espClient);

unsigned long lastMsg = 0;
const long INTERVAL = 10000; // Gửi dữ liệu định kỳ mỗi 10 giây

void setupWifi() {
  delay(10);
  Serial.print("Connecting to WiFi: ");
  Serial.println(WIFI_SSID);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi connected! IP: ");
  Serial.println(WiFi.localIP());
}

void reconnectMqtt() {
  while (!client.connected()) {
    Serial.print("Dang ket noi MQTT Broker...");
    String clientId = "ESP32-" + String(HARDWARE_ID) + "-" + String(random(0xffff), HEX);
    if (client.connect(clientId.c_str())) {
      Serial.println(" -> KET NOI THANH CONG!");
    } else {
      Serial.print(" -> That bai, rc=");
      Serial.print(client.state());
      Serial.println(". Thu lai sau 3s...");
      delay(3000);
    }
  }
}

void setup() {
  Serial.begin(115200);
  dht.setup(DHT_PIN, DHTesp::DHT22);
  setupWifi();
  
  client.setServer(MQTT_BROKER, MQTT_PORT);
  client.setKeepAlive(60);      // Giữ kết nối 60s, không bị timeout do Wokwi chạy chậm
  client.setSocketTimeout(15);
  
  sprintf(topicReading, "warehouse/%s/zone/%s/sensor/%s/reading", 
          WAREHOUSE_ID, ZONE_ID, HARDWARE_ID);
}

void loop() {
  if (!client.connected()) {
    reconnectMqtt();
  }
  client.loop();

  unsigned long now = millis();
  if (now - lastMsg > INTERVAL) {
    lastMsg = now;
    
    TempAndHumidity data = dht.getTempAndHumidity();
    if (isnan(data.temperature) || isnan(data.humidity)) {
      Serial.println("Loi doc cam bien DHT22!");
      return;
    }

    StaticJsonDocument<256> doc;
    doc["temperature"] = serialized(String(data.temperature, 2));
    doc["humidity"]    = serialized(String(data.humidity, 2));
    doc["battery"]     = 95;
    doc["rssi"]        = WiFi.RSSI();

    char buffer[256];
    serializeJson(doc, buffer);

    bool ok = client.publish(topicReading, buffer);
    if (ok) {
      Serial.print("[THANH CONG] ");
      Serial.print(HARDWARE_ID);
      Serial.print(" -> ");
      Serial.println(buffer);
    } else {
      Serial.println("[THAT BAI] Khong gui duoc goi tin!");
    }
  }

  delay(10); // Nghỉ 10ms tránh nghẽn CPU ảo trên trình duyệt
}
