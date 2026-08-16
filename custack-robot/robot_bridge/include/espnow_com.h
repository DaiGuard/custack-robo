#ifndef ESPNOW_COM_H
#define ESPNOW_COM_H

#include <Arduino.h>
#include <stdint.h>
#include "espnow_protocol.h"

class ESPNowCom {
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
     * @brief ESPNOW通信の更新
     */
    bool update(uint32_t now);

    /**
     * @brief ESPNOW通信の接続状態を取得
     */
    bool isConnected();

    void setVelocity(int vx, int vy, int omega);
    void setArm(int rarm, int larm);
    void setStop();
    void getPacket(int* vx, int* vy, int* omega, 
        int* rarm, int* larm, int* watchdog);

    // テレメトリ受信アクセサ
    bool hasNewTelemetry();
    bool getTelemetry(EspNowTelemetryPacket* out_tlm);

    /**
     * @brief ペアMACアドレスの登録
     */
    bool registerPeer(const String& mac);

    String getOwnMac();
    String getTagMac();
    bool setTagMac(const String& mac);

    void macStr2Byte(const String& mac, uint8_t* mac_bytes);
    void macByte2Str(const uint8_t* mac_bytes, String& mac);
};

#endif // ESPNOW_COM_H