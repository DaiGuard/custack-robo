#include <Arduino.h>
#include <WiFi.h>
#include <esp_now.h>
#include <esp_wifi.h>
#include "espnow_com.h"

uint32_t g_last_recv_ms = 0u;

EspNowCommandPacket g_packet = {
    .vx = 0,
    .vy = 0,
    .omega = 0,
    .arm_right = 0,
    .arm_left = 0,
    .watchdog = 0,
};

ESPNowCom::ESPNowCom() {
    // 
    _own_mac_address = "";
}

bool ESPNowCom::begin() {
    // ESP-NOW通信の初期化
    WiFi.mode(WIFI_STA);
    WiFi.disconnect();

    esp_wifi_set_promiscuous(true);
    esp_wifi_set_channel(1, WIFI_SECOND_CHAN_NONE);
    esp_wifi_set_promiscuous(false);

    // 自身のMACアドレスを取得
    _own_mac_address = WiFi.macAddress();

    if (esp_now_init() != ESP_OK) {
        return false;
    }

    esp_now_register_recv_cb([](const uint8_t *mac, const uint8_t *data, int len) {
        if (len == sizeof(EspNowCommandPacket)) {
            memcpy(&g_packet, data, sizeof(EspNowCommandPacket));
            g_last_recv_ms = millis();
        }
    });

    return true;
}

bool ESPNowCom::update(uint32_t now, EspNowCommandPacket* data) {
    if(now - g_last_recv_ms > 1000) { 
        return false;
    }

    memcpy(data, &g_packet, sizeof(EspNowCommandPacket));

    return true;
}