#ifndef ESPNOW_COM_H
#define ESPNOW_COM_H


#include <stdint.h>
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
     * 
     */
    bool begin();

    /**
     * @brief ESPNOW通信の更新
     * 
     */
    bool update(uint32_t now, EspNowCommandPacket* data);

    String getOwnMacAddress() { return _own_mac_address; }
};

#endif // ESPNOW_COM_H