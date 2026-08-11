#ifndef ESPNOW_COM_H
#define ESPNOW_COM_H


class ESPNowCom {
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
     * @return true
     * @return false
     */
    bool update(uint32_t now);

    /**
     * @brief ESPNOW通信の接続状態を取得
     * 
     * @return true 
     * @return false 
     */
    bool isConnected();

    void setVelocity(int vx, int vy, int omega);
    void setArm(int rarm, int larm);
    void setStop();
    void getPacket(int* vx, int* vy, int* omega, 
        int* rarm, int* larm, int* watchdog);
    /**
     * @brief ペアMACアドレスの登録
     * 
     * @param mac 
     * @return true 
     * @return false 
     */
    bool registerPeer(const String mac);

    /**
     * @brief 自身のMACアドレスを取得
     * 
     * @return String 自身のMACアドレス
     */
    String getOwnMac();

    /**
     * @brief ターゲットのMACアドレスを取得
     * 
     * @return String ターゲットのMACアドレス
     */
    String getTagMac();

    /**
     * @brief ターゲットのMACアドレスを設定
     * 
     * @param mac ターゲットのMACアドレス
     */
    void setTagMac(String mac);

    /**
     * @brief MACアドレス文字列をバイト列に変換
     * 
     * @param mac 
     * @param mac_bytes 
     */
    void macStr2Byte(String mac, uint8_t* mac_bytes);

    /**
     * @brief バイト列をMACアドレスに変換
     * 
     * @param mac_bytes 
     * @param mac 
     */
    void macByte2Str(uint8_t* mac_bytes, String& mac);

};

#endif // ESPNOW_COM_H