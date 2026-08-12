#ifndef I2C_PROTOCOL_H
#define I2C_PROTOCOL_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>

#define I2C_FREQ 10000

// ==========================================
// I2Cスレーブアドレス定義
// ==========================================
#define I2C_ADDR_LEG        0x30  // 脚ユニット
#define I2C_ADDR_ARM_RIGHT  0x31  // 右アームユニット
#define I2C_ADDR_ARM_LEFT   0x32  // 左アームユニット

// ==========================================
// 共通レジスタ定義
// ==========================================
#define REG_DEVICE_ID       0x00
#define REG_FW_VERSION      0x01
#define REG_WATCHDOG        0x02

// ==========================================
// 脚ユニット用レジスタ定義
// ==========================================
#define REG_LEG_VX_L        0x10
#define REG_LEG_VX_H        0x11
#define REG_LEG_VY_L        0x12
#define REG_LEG_VY_H        0x13
#define REG_LEG_OMEGA_L     0x14
#define REG_LEG_OMEGA_H     0x15

// ==========================================
// アームユニット用レジスタ定義
// ==========================================
#define REG_ARM_DUTY        0x10
#define REG_ARM_PATTERN     0x11

// アームユニット制約・設定値定数
#define ARM_DUTY_MAX        15   // 最大Duty比 (約60%クランプ)

#define ARM_PATTERN_MANUAL  0x00  // 0: 手動Duty
#define ARM_PATTERN_PULSE   0x01  // 1: 単発パルス
#define ARM_PATTERN_BLINK   0x02  // 2: 点滅振動

// ==========================================
// I2C送受信用データ構造体
// ==========================================
#pragma pack(push, 1)

// 脚ユニット用データ構造体 (6Bytes)
// マスターからの連続書き込み (Auto-Increment) 送信用
typedef struct {
    int16_t vx;     // -1000 〜 1000
    int16_t vy;     // -1000 〜 1000
    int16_t omega;  // -1000 〜 1000
} I2cLegData;

// アームユニット用データ構造体 (2Bytes)
// マスターからの連続書き込み (Auto-Increment) 送信用
typedef struct {
    uint8_t duty;    // 0 〜 255 (内部で最大153にクランプ)
    uint8_t pattern; // ARM_PATTERN_XXX を指定
} I2cArmData;

#pragma pack(pop)

#ifdef __cplusplus
}
#endif

#endif // I2C_PROTOCOL_H