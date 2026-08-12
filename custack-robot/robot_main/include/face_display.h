#ifndef FACE_DISPLAY_H
#define FACE_DISPLAY_H

#include <M5Unified.h>
#include "robot_status.h"


class FaceDisplay {
private:
    int _width;
    int _height;
    int _center_x;
    int _center_y;

    M5Canvas _canvas;

public:
    /**
     * @brief コンストラクタ
     */
    FaceDisplay();

    /**
     * @brief 顔表示の初期化
     */
    void begin();

    /**
     * @brief 顔表示の更新
     * 
     * @param face_type 表情のタイプ
     * @param now 現在時間
     */
    void update(int face_type, uint32_t now,
        bool info_enable, RobotStatus* robot_status);

    /**
     * @brief 顔表情のクリア
     * 
     */
    void clear();

    /**
     * @brief 待機表情を描画
     * 
     * @param now 
     */
    void idleFace(uint32_t now);

    /**
     * @brief 戦闘表情を描画
     * 
     * @param now 
     */
    void battleFace(uint32_t now);

    /**
     * @brief バッテリ情報表示
     * 
     * @param status 
     * @param batt 
     */
    void batteryDisplay(int status, float batt);

    /**
     * @brief 疲れた表情を描画
     * 
     * @param now 
     */
    void tiredFace(uint32_t now);

    /**
     * @brief ESP-NOW通信状態表示
     * 
     * @param status
     */
    void espnowDisplay(bool status);

    /**
     * @brief 5V供給表示
     * 
     * @param sw 
     */
    void poweronDisplay(bool sw);

    /**
     * @brief 通信データの表示
     * 
     * @param vx 
     * @param vy 
     * @param omega 
     * @param rarm 
     * @param larm 
     * @param watchdog 
     */
    void comdataDisplay(
        int16_t vx, int16_t vy, int16_t omega,
        uint8_t rarm, uint8_t larm,
        uint8_t watchdog
    );

    /**
     * @brief MACアドレス表示
     * 
     * @param mac 
     */
    void macDisplay(String& mac);

    /**
     * @brief ユニットステータス表示
     * 
     * @param leg_status 
     * @param rarm_status 
     * @param larm_status 
     */
    void unitstatusDisplay(
        bool leg_status,
        bool rarm_status,
        bool larm_status
    );

    /**
     * @brief 起動音
     * 
     */
    void wakeupSound();

    /**
     * @brief 情報表示音
     * 
     */
    void infoSound(bool sw);

    /**
     * @brief パワーオン音
     * 
     */
    void powerSound(bool sw);
};

#endif // FACE_DISPLAY_H