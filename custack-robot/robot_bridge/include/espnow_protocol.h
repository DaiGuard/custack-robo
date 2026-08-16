#ifndef ESPNOW_PROTOCOL_H
#define ESPNOW_PROTOCOL_H

#include <stdint.h>

#pragma pack(push, 1)

// ESP-NOW 上位制御 16Byte 固定長パケット (Bridge -> Core2, 100Hz)
typedef struct {
    int16_t vx;          // 移動速度Vx (-1000 〜 1000)
    int16_t vy;          // 移動速度Vy (-1000 〜 1000)
    int16_t omega;       // 移動速度Omega (-1000 〜 1000)
    uint8_t arm_right;   // 右アームユニット起動フラグ (0x00:停止, 0x01:起動)
    uint8_t arm_left;    // 左アームユニット起動フラグ (0x00:停止, 0x01:起動)
    uint8_t reserved[7]; // 予約領域 (7Bytes padding)
    uint8_t watchdog;    // ウォッチドッグ (送信毎にインクリメント)
} EspNowCommandPacket;

// ESP-NOW テレメトリ 16Byte 固定長パケット (Core2 -> Bridge, 100ms周期)
typedef struct {
    uint8_t leg_id;        // 脚ユニット デバイスID (0x01: Omni, 0x02: Tire, 0x03: Crawler)
    uint8_t arm_right_id;  // 右アーム デバイスID (0x01: Pistol, 0x02: Gatling, 0x03: Missile)
    uint8_t arm_left_id;   // 左アーム デバイスID (0x01: Pistol, 0x02: Gatling, 0x03: Missile)
    uint8_t status_flags;  // 状態フラグ (bit0: DCDC_5V_ON, bit1: BatLowWarning, bit2: BatCritical)
    uint16_t battery_mv;   // バッテリー電圧 (mV単位, 例: 7400)
    uint8_t reserved[9];   // 予約パディング領域
    uint8_t watchdog;      // テレメトリウォッチドッグ
} EspNowTelemetryPacket;

#pragma pack(pop)

// 定数定義
#define ESPNOW_ARM_STOP     0x00
#define ESPNOW_ARM_START    0x01

#define LEG_ID_NONE         0x00
#define LEG_ID_OMNI         0x01
#define LEG_ID_TIRE         0x02
#define LEG_ID_CRAWLER      0x03

#define ARM_ID_NONE         0x00
#define ARM_ID_PISTOL       0x01
#define ARM_ID_GATLING      0x02
#define ARM_ID_MISSILE      0x03

#endif // ESPNOW_PROTOCOL_H
