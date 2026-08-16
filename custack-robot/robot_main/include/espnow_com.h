#ifndef ESPNOW_COM_H
#define ESPNOW_COM_H

#include <stdint.h>
#include <Arduino.h>
#include "espnow_protocol.h"

class ESPNowCom {
private:
    String _own_mac_address;

public:
    /**
     * @brief コンストラクタ
     */
    ESPNowCom();

    /**
     * @brief ESPNOW通信の初期化
     */
    bool begin();

    /**
     * @brief ESPNOW通信の更新 (受信データの取得 & タイムアウト処理)
     */
    bool update(uint32_t now, EspNowCommandPacket* data);

    /**
     * @brief テレメトリパケットをBridgeへ送信
     */
    bool sendTelemetry(const EspNowTelemetryPacket* tlm);

    String getOwnMacAddress() const { return _own_mac_address; }
};

#endif // ESPNOW_COM_H