#include <Arduino.h>
#include <WiFi.h>
#include <esp_now.h>
#include <esp_wifi.h>
#include "espnow_com.h"

uint32_t g_last_recv_ms = 0u;
uint8_t g_own_mac_addr[6] = {0};
uint8_t g_host_mac_addr[6] = {0};
bool g_host_peer_added = false;

EspNowCommandPacket g_packet = {
    .vx = 0,
    .vy = 0,
    .omega = 0,
    .arm_right = 0,
    .arm_left = 0,
    .watchdog = 0,
};

ESPNowCom::ESPNowCom() {
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
    WiFi.macAddress(g_own_mac_addr);

    if (esp_now_init() != ESP_OK) {
        return false;
    }

    esp_now_register_recv_cb([](const uint8_t *mac, const uint8_t *data, int len) {
        // 自分自身が送信したパケットは無視する
        if (memcmp(mac, g_own_mac_addr, 6) != 0) {
            // 送信元のMACアドレスを保存し、未登録ならピア登録
            if (memcmp(g_host_mac_addr, mac, 6) != 0 || !g_host_peer_added) {
                memcpy(g_host_mac_addr, mac, 6);
                esp_now_peer_info_t peerInfo = {};
                memcpy(peerInfo.peer_addr, mac, 6);
                peerInfo.channel = 1;
                peerInfo.encrypt = false;
                if (!esp_now_is_peer_exist(mac)) {
                    esp_now_add_peer(&peerInfo);
                }
                g_host_peer_added = true;
            }

            if (len == sizeof(EspNowCommandPacket)) {
                memcpy(&g_packet, data, sizeof(EspNowCommandPacket));
                g_last_recv_ms = millis();
            }
        }
    });

    return true;
}

bool ESPNowCom::update(uint32_t now, EspNowCommandPacket* data) {
    if(now - g_last_recv_ms > 1000) { 
        EspNowCommandPacket stop_packet = {
            .vx = 0,
            .vy = 0,
            .omega = 0,
            .arm_right = 0,
            .arm_left = 0,
            .watchdog = 0,
        };

        memcpy(data, &stop_packet, sizeof(EspNowCommandPacket));
        return false;
    }

    memcpy(data, &g_packet, sizeof(EspNowCommandPacket));
    return true;
}

bool ESPNowCom::sendTelemetry(const EspNowTelemetryPacket* tlm) {
    if (!g_host_peer_added) return false;
    esp_err_t res = esp_now_send(g_host_mac_addr, (const uint8_t*)tlm, sizeof(EspNowTelemetryPacket));
    return (res == ESP_OK);
}