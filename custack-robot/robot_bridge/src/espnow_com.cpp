#include <Arduino.h>
#include <WiFi.h>
#include <esp_now.h>
#include <esp_wifi.h>
#include "espnow_com.h"
#include "espnow_protocol.h"

namespace {
    // 自身のMACアドレス
    String own_mac = "FF:FF:FF:FF:FF:FF";
    uint8_t own_mac_bytes[6] = {
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
    };

    // ターゲットのMACアドレス
    String tag_mac = "3C:8A:1F:D7:49:C0";
    uint8_t tag_mac_bytes[6] = {
        0x3C, 0x8A, 0x1F, 0xD7, 0x49, 0xC0
    };

    // 通信パケット
    EspNowCommandPacket packet;

    // 通信状態
    bool is_connected = false;
}

ESPNowCom::ESPNowCom() {
    // コンストラクタ (必要に応じて初期化処理を記述)
    packet.vx = 0;
    packet.vy = 0;
    packet.omega = 0;
    packet.arm_right = ESPNOW_ARM_STOP;
    packet.arm_left = ESPNOW_ARM_STOP;
    packet.watchdog = 0;
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

    if (esp_now_init() != ESP_OK) {
        return false;
    }

    // 送信コールバック関数の登録
    esp_now_register_send_cb([](const uint8_t* mac_addr, esp_now_send_status_t status) {
        is_connected = (status == ESP_NOW_SEND_SUCCESS);
    });

    // 通信ペアの登録
    if (!registerPeer(tag_mac)) {
        return false;
    }
    macStr2Byte(tag_mac, tag_mac_bytes);

    return true;
}

bool ESPNowCom::registerPeer(const String mac) {

    // 登録済みのMACアドレスを確認
    if (esp_now_is_peer_exist(tag_mac_bytes)) {
        esp_now_del_peer(tag_mac_bytes);
    }

    // MACアドレス文字列をバイト列に変換
    uint8_t mac_addr[6];
    macStr2Byte(mac, (uint8_t*)mac_addr);

    // 新しいMACアドレスを登録する
    esp_now_peer_info_t peerInfo = {};
    memcpy(peerInfo.peer_addr, mac_addr, 6);
    peerInfo.channel = 1;
    peerInfo.encrypt = false;
    if(esp_now_add_peer(&peerInfo) == ESP_OK) {
        tag_mac = mac;
        macStr2Byte(tag_mac, tag_mac_bytes);
        return true;
    }

    return false;
}

void ESPNowCom::macStr2Byte(String mac, uint8_t* mac_bytes) {
    sscanf(mac.c_str(),
        "%hhx:%hhx:%hhx:%hhx:%hhx:%hhx",
        &mac_bytes[0], &mac_bytes[1], &mac_bytes[2],
        &mac_bytes[3], &mac_bytes[4], &mac_bytes[5]);
}

void ESPNowCom::macByte2Str(uint8_t* mac_bytes, String& mac) {
    char mac_str[18]; // 17 characters for MAC address + 1 for null terminator
    sprintf(mac_str, "%02X:%02X:%02X:%02X:%02X:%02X",
        mac_bytes[0], mac_bytes[1], mac_bytes[2],
        mac_bytes[3], mac_bytes[4], mac_bytes[5]);
    mac = String(mac_str);
}


bool ESPNowCom::update(uint32_t now) {
    // ESPNOW通信の更新処理
    static uint32_t last_espnow_ms = 0;
    if (now - last_espnow_ms < 10) { return true; }
    last_espnow_ms = now;

    // ウォッチドック更新
    packet.watchdog++;

    // ESP-NOWデータ送信
    esp_err_t result = esp_now_send(
        (uint8_t*)tag_mac_bytes,
        (uint8_t*)&packet,
        sizeof(packet));
    if (result != ESP_OK) {
        is_connected = false;
        return false;
    }

    return true;
}

bool ESPNowCom::isConnected() {
    return is_connected;
}

void ESPNowCom::setVelocity(int vx, int vy, int omega) {
    packet.vx = (int16_t)constrain(vx, -1000, 1000);
    packet.vy = (int16_t)constrain(vy, -1000, 1000);
    packet.omega = (int16_t)constrain(omega, -1000, 1000);
}

void ESPNowCom::setArm(int rarm, int larm) {
    packet.arm_right = (uint8_t)(rarm > 0 ? ESPNOW_ARM_START : ESPNOW_ARM_STOP);
    packet.arm_left = (uint8_t)(larm > 0 ? ESPNOW_ARM_START : ESPNOW_ARM_STOP);
}

void ESPNowCom::setStop() {
    packet.vx = (int16_t)0;
    packet.vy = (int16_t)0;
    packet.omega = (int16_t)0;
    packet.arm_right = ESPNOW_ARM_STOP;
    packet.arm_left = ESPNOW_ARM_STOP;
}

void ESPNowCom::getPacket(int* vx, int* vy, int* omega, 
    int* rarm, int* larm, int* watchdog) {
    *vx = packet.vx;
    *vy = packet.vy;
    *omega = packet.omega;
    *rarm = packet.arm_right;
    *larm = packet.arm_left;
    *watchdog = packet.watchdog;
}

String ESPNowCom::getOwnMac() {
    return own_mac;
}

String ESPNowCom::getTagMac() {
    return tag_mac;
}

void ESPNowCom::setTagMac(String mac) {
    tag_mac = mac;
}

