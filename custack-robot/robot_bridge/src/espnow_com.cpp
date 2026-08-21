#include <Arduino.h>
#include <WiFi.h>
#include <esp_now.h>
#include <esp_wifi.h>
#include <EEPROM.h>
#include "espnow_com.h"
#include "espnow_protocol.h"

namespace {
    // EEPROM設定
    constexpr int EEPROM_SIZE = 32;
    constexpr int ADDR_MAC_VALID = 0;
    constexpr int ADDR_TAG_MAC = 1;

    // 自身のMACアドレス
    String own_mac = "FF:FF:FF:FF:FF:FF";
    uint8_t own_mac_bytes[6] = {
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
    };

    // ターゲットのMACアドレス
    String tag_mac = "3C:8A:1F:D7:49:C0"; // フォールバック用のデフォルト値
    uint8_t tag_mac_bytes[6] = {
        0x3C, 0x8A, 0x1F, 0xD7, 0x49, 0xC0
    };

    // 通信パケット
    EspNowCommandPacket packet;

    // 受信テレメトリ
    EspNowTelemetryPacket recv_tlm;
    bool has_new_tlm = false;

    // 通信状態
    bool is_connected = false;
}

ESPNowCom::ESPNowCom() {
    packet.vx = 0;
    packet.vy = 0;
    packet.omega = 0;
    packet.arm_right = ESPNOW_ARM_STOP;
    packet.arm_left = ESPNOW_ARM_STOP;
    packet.watchdog = 0;

    memset(&recv_tlm, 0, sizeof(EspNowTelemetryPacket));
}

bool ESPNowCom::begin() {
    // ESPNOW通信の初期化
    WiFi.mode(WIFI_STA);
    WiFi.disconnect();

    esp_wifi_set_promiscuous(true);
    esp_wifi_set_channel(1, WIFI_SECOND_CHAN_NONE);
    esp_wifi_set_promiscuous(false);

    // 自身のMACアドレスを取得
    own_mac = WiFi.macAddress();
    macStr2Byte(own_mac, own_mac_bytes);

    // EEPROMからMACアドレスを読み込み
    EEPROM.begin(EEPROM_SIZE);
    if (EEPROM.read(ADDR_MAC_VALID) == 0x01) {
        EEPROM.get(ADDR_TAG_MAC, tag_mac_bytes);
        macByte2Str(tag_mac_bytes, tag_mac);
    } else {
        // 有効なMACアドレスがなければデフォルト値を書き込む
        macStr2Byte(tag_mac, tag_mac_bytes);
    }

    if (esp_now_init() != ESP_OK) {
        return false;
    }

    // 送信コールバック関数の登録
    esp_now_register_send_cb([](const uint8_t* mac_addr, esp_now_send_status_t status) {
        is_connected = (status == ESP_NOW_SEND_SUCCESS);
    });

    // 受信コールバック関数の登録 (Core2からのテレメトリ受信)
    esp_now_register_recv_cb([](const uint8_t *mac, const uint8_t *data, int len) {
        if (len == sizeof(EspNowTelemetryPacket)) {
            memcpy(&recv_tlm, data, sizeof(EspNowTelemetryPacket));
            has_new_tlm = true;
        }
    });

    // 通信ペアの登録
    if (!registerPeer(tag_mac)) {
        return false;
    }

    return true;
}

bool ESPNowCom::registerPeer(const String& mac) {
    uint8_t mac_addr[6];
    macStr2Byte(mac, mac_addr);

    // 既存の登録ピアを削除
    if (esp_now_is_peer_exist(tag_mac_bytes)) {
        esp_now_del_peer(tag_mac_bytes);
    }
    if (esp_now_is_peer_exist(mac_addr)) {
        esp_now_del_peer(mac_addr);
    }

    esp_now_peer_info_t peerInfo = {};
    memcpy(peerInfo.peer_addr, mac_addr, 6);
    peerInfo.channel = 1;
    peerInfo.encrypt = false;

    if (esp_now_add_peer(&peerInfo) != ESP_OK) {
        return false;
    }

    // 成功時にターゲットMAC情報を更新
    memcpy(tag_mac_bytes, mac_addr, 6);
    macByte2Str(tag_mac_bytes, tag_mac);

    return true;
}

bool ESPNowCom::update(uint32_t now) {
    static uint32_t last_send_time = 0;
    uint32_t update_span = 10u;

    if (is_connected) {
        update_span = 10u;
    } else {
        update_span = 100u;
    }

    if (now - last_send_time < update_span) {
        return true;
    }
    last_send_time = now;

    packet.watchdog++;

    esp_err_t result = esp_now_send(tag_mac_bytes, (uint8_t*)&packet, sizeof(EspNowCommandPacket));
    return (result == ESP_OK);
}

bool ESPNowCom::isConnected() {
    return is_connected;
}

void ESPNowCom::setVelocity(int vx, int vy, int omega) {
    packet.vx = constrain(vx, -1000, 1000);
    packet.vy = constrain(vy, -1000, 1000);
    packet.omega = constrain(omega, -1000, 1000);
}

void ESPNowCom::setArm(int rarm, int larm) {
    packet.arm_right = (rarm > 0) ? ESPNOW_ARM_START : ESPNOW_ARM_STOP;
    packet.arm_left = (larm > 0) ? ESPNOW_ARM_START : ESPNOW_ARM_STOP;
}

void ESPNowCom::setStop() {
    packet.vx = 0;
    packet.vy = 0;
    packet.omega = 0;
    packet.arm_right = ESPNOW_ARM_STOP;
    packet.arm_left = ESPNOW_ARM_STOP;
}

void ESPNowCom::getPacket(int* vx, int* vy, int* omega, int* rarm, int* larm, int* watchdog) {
    *vx = packet.vx;
    *vy = packet.vy;
    *omega = packet.omega;
    *rarm = packet.arm_right;
    *larm = packet.arm_left;
    *watchdog = packet.watchdog;
}

bool ESPNowCom::hasNewTelemetry() {
    return has_new_tlm;
}

bool ESPNowCom::getTelemetry(EspNowTelemetryPacket* out_tlm) {
    if (out_tlm != nullptr) {
        memcpy(out_tlm, &recv_tlm, sizeof(EspNowTelemetryPacket));
    }
    bool had_new = has_new_tlm;
    has_new_tlm = false;
    return had_new;
}

String ESPNowCom::getOwnMac() {
    return own_mac;
}

String ESPNowCom::getTagMac() {
    return tag_mac;
}

bool ESPNowCom::setTagMac(const String& mac) {
    if (mac.length() < 17) {
        return false;
    }

    if (!registerPeer(mac)) {
        return false;
    }

    EEPROM.write(ADDR_MAC_VALID, 0x01);
    EEPROM.put(ADDR_TAG_MAC, tag_mac_bytes);
    EEPROM.commit();

    return true;
}

void ESPNowCom::macStr2Byte(const String& mac, uint8_t* mac_bytes) {
    for (int i = 0; i < 6; i++) {
        mac_bytes[i] = strtol(mac.substring(i * 3, i * 3 + 2).c_str(), NULL, 16);
    }
}

void ESPNowCom::macByte2Str(const uint8_t* mac_bytes, String& mac) {
    mac = "";
    for (int i = 0; i < 6; i++) {
        if (mac_bytes[i] < 0x10) {
            mac += "0";
        }
        mac += String(mac_bytes[i], HEX);
        if (i < 5) {
            mac += ":";
        }
    }
    mac.toUpperCase();
}
