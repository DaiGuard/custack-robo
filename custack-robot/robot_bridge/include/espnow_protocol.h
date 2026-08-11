#ifndef ESPNOW_PROTOCOL_H
#define ESPNOW_PROTOCOL_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>

// ==========================================
// ESP-NOW 制御定数
// ==========================================
#define ESPNOW_ARM_STOP   0x00
#define ESPNOW_ARM_START  0x01

// ==========================================
// ESP-NOW 通信パケット定義
// ==========================================
#pragma pack(push, 1)

// ESP-NOW 上位通信用 16Byte固定長パケット構造体
typedef struct {
    int16_t vx;          // 移動速度Vx (-1000 〜 1000)
    int16_t vy;          // 移動速度Vy (-1000 〜 1000)
    int16_t omega;       // 移動速度Omega (-1000 〜 1000)
    uint8_t arm_right;   // 右アームユニット起動フラグ (0x00:停止, 0x01:起動)
    uint8_t arm_left;    // 左アームユニット起動フラグ (0x00:停止, 0x01:起動)
    uint8_t reserved[7]; // 予約領域 (7Bytes padding)
    uint8_t watchdog;    // ウォッチドッグ (上位から周期的にインクリメント)
} EspNowCommandPacket;

#pragma pack(pop)

#ifdef __cplusplus
}
#endif

#endif // ESPNOW_PROTOCOL_H
