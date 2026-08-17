#include <Arduino.h>
#include <Wire.h>
#include <Servo.h>
#include <EEPROM.h>
#include "i2c_protocol.h"

// -----------------------------------------------------------------------------
// ハードウェア・ピン定義 (ATtiny1614)
// -----------------------------------------------------------------------------
#define PIN_PWM1   PIN_PA4
#define PIN_PWM2   PIN_PA5
#define PIN_PWM3   PIN_PA6
#define PIN_PWM4   PIN_PA7

// -----------------------------------------------------------------------------
// 各車輪の回転方向係数 (+1: 通常, -1: 反転)
// -----------------------------------------------------------------------------
#define DIR1  (+1)
#define DIR2  (+1)
#define DIR3  (+1)
#define DIR4  (+1)

// -----------------------------------------------------------------------------
// サーボインスタンス
// -----------------------------------------------------------------------------
static Servo servo1; // PWM1 (PA4)
static Servo servo2; // PWM2 (PA5)
static Servo servo3; // PWM3 (PA6)
static Servo servo4; // PWM4 (PA7)

// -----------------------------------------------------------------------------
// 定数設定
// -----------------------------------------------------------------------------
#define SERVO_NEUTRAL_US 1500  // 停止時ニュートラルパルス幅
#define SERVO_MIN_US     1000  // 最大逆転パルス幅
#define SERVO_MAX_US     2000  // 最大正転パルス幅
#define WD_TIMEOUT_MS    1000  // ウォッチドックタイムアウト時間

// 20% 速度パルス幅 (1600us相当: 速度 +200)
#define TEST_SPEED_VAL   200

// -----------------------------------------------------------------------------
// サーボパラメータ
// -----------------------------------------------------------------------------
static volatile int16_t offset1 = 0;
static volatile int16_t offset2 = 0;
static volatile int16_t offset3 = 0;
static volatile int16_t offset4 = 0;
static volatile uint8_t scale1 = 0u;
static volatile uint8_t scale2 = 0u;
static volatile uint8_t scale3 = 0u;
static volatile uint8_t scale4 = 0u;

// -----------------------------------------------------------------------------
// レジスタマップ
// -----------------------------------------------------------------------------
static volatile uint8_t registers[REG_LEG_MAPSIZE] = {0};
static volatile uint8_t current_reg_addr = 0;

// -----------------------------------------------------------------------------
// 変数
// -----------------------------------------------------------------------------
static volatile uint32_t last_wd_update_time = 0;
static volatile uint8_t last_wd_value = 0;

// -----------------------------------------------------------------------------
template <typename T>
static inline T clamp_val(T val, T min_val, T max_val) {
    if (val < min_val) return min_val;
    if (val > max_val) return max_val;
    return val;
}

int16_t eeprom_read_i16(uint8_t address) {
    int16_t value = 0;
    // for (int i = 0; i < 2; i++) {
    //     value <<= 8;
    //     value |= EEPROM.read(address + i);
    // }
    EEPROM.get(address, value);
    return value;
}

uint8_t eeprom_read_u8(uint8_t address) {
    uint8_t value = 0u;
    value = EEPROM.read(address);
    return value;
}

void eeprom_write_i16(uint8_t address, int16_t val) {
    EEPROM.put(address, val);
}

void eeprom_write_u8(uint8_t address, uint8_t val) {
    EEPROM.update(address, val);
}

// MASTERからの書き込みリクエスト
void receiveEvent(int howMany) {
    // SMBus Quick Write (スキャンモード)やデータなしの場合
    if (howMany <= 0) { return; }

    // レジスタアドレス指定
    current_reg_addr = Wire.read();
    howMany--;

    // データを更新
    uint8_t addr = current_reg_addr;
    while (howMany > 0 && Wire.available()) {
        uint8_t val = Wire.read();
        if (addr < REG_LEG_MAPSIZE) {
            // ウォッチドック値が更新された場合
            if (addr == REG_WATCHDOG && last_wd_value != val) {
                // データ受信時の時刻を更新
                last_wd_update_time = millis();
                last_wd_value = val;
            }

            registers[addr] = val;
            addr++;
        }
        howMany--;
    }
}

// MASTERからの読み出しリクエスト
void requestEvent() {
    if (current_reg_addr < REG_LEG_MAPSIZE) {
        Wire.write(registers[current_reg_addr]);
        current_reg_addr++;
    } else {
        Wire.write(0x00);
    }
}

void setup() {
    // EEPROMから値の読み出し
    offset1 = eeprom_read_i16(0x00);
    offset2 = eeprom_read_i16(0x02);
    offset3 = eeprom_read_i16(0x04);
    offset4 = eeprom_read_i16(0x06);
    scale1 = eeprom_read_u8(0x08);
    scale2 = eeprom_read_u8(0x09);
    scale3 = eeprom_read_u8(0x0A);
    scale4 = eeprom_read_u8(0x0B);

    // 未初期化 (0xFF) の場合のフォールバック
    if (scale1 == 0xFF) scale1 = 100;
    if (scale2 == 0xFF) scale2 = 100;
    if (scale3 == 0xFF) scale3 = 100;
    if (scale4 == 0xFF) scale4 = 100;

    // レジスタ初期値設定
    registers[REG_DEVICE_ID]    = DEVICE_ID;
    registers[REG_FW_VERSION]   = FW_VERSION;
    registers[REG_LEG_OFFSET1_L] = offset1 & 0xFF;
    registers[REG_LEG_OFFSET1_H] = (offset1 >> 8) & 0xFF;
    registers[REG_LEG_OFFSET2_L] = offset2 & 0xFF;
    registers[REG_LEG_OFFSET2_H] = (offset2 >> 8) & 0xFF;
    registers[REG_LEG_OFFSET3_L] = offset3 & 0xFF;
    registers[REG_LEG_OFFSET3_H] = (offset3 >> 8) & 0xFF;
    registers[REG_LEG_OFFSET4_L] = offset4 & 0xFF;
    registers[REG_LEG_OFFSET4_H] = (offset4 >> 8) & 0xFF;
    registers[REG_LEG_SCALE1]   = scale1;
    registers[REG_LEG_SCALE2]   = scale2;
    registers[REG_LEG_SCALE3]   = scale3;
    registers[REG_LEG_SCALE4]   = scale4;

    // サーボアタッチ
    servo1.attach(PIN_PWM1, SERVO_MIN_US, SERVO_MAX_US);
    servo2.attach(PIN_PWM2, SERVO_MIN_US, SERVO_MAX_US);
    servo3.attach(PIN_PWM3, SERVO_MIN_US, SERVO_MAX_US);
    servo4.attach(PIN_PWM4, SERVO_MIN_US, SERVO_MAX_US);

    // サーボ初期状態
    servo1.writeMicroseconds(SERVO_NEUTRAL_US + offset1);
    servo2.writeMicroseconds(SERVO_NEUTRAL_US + offset2);
    servo3.writeMicroseconds(SERVO_NEUTRAL_US + offset3);
    servo4.writeMicroseconds(SERVO_NEUTRAL_US + offset4);

    // テスト動作: 50%の速度で1000msec回転 (スケール・オフセット適用)
#if DEVICE_ID == 0x02
    uint16_t test1 = clamp_val((uint16_t)(SERVO_NEUTRAL_US + offset1 + (((int32_t)500 * (+1) * scale1) / 100)), (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    uint16_t test3 = clamp_val((uint16_t)(SERVO_NEUTRAL_US + offset3 + (((int32_t)500 * (-1) * scale3) / 100)), (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);

    servo1.writeMicroseconds(test1);
    servo3.writeMicroseconds(test3);
    delay(1000);
#elif DEVICE_ID == 0x03
    uint16_t test1 = clamp_val((uint16_t)(SERVO_NEUTRAL_US + offset1 + (((int32_t)500 * (-1) * scale1) / 100)), (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    uint16_t test3 = clamp_val((uint16_t)(SERVO_NEUTRAL_US + offset3 + (((int32_t)500 * (+1) * scale3) / 100)), (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);

    servo1.writeMicroseconds(test1);
    servo3.writeMicroseconds(test3);
    delay(1000);
#else
    uint16_t test1 = clamp_val((uint16_t)(SERVO_NEUTRAL_US + offset1 + (((int32_t)500 * DIR1 * scale1) / 100)), (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    uint16_t test2 = clamp_val((uint16_t)(SERVO_NEUTRAL_US + offset2 + (((int32_t)500 * DIR2 * scale2) / 100)), (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    uint16_t test3 = clamp_val((uint16_t)(SERVO_NEUTRAL_US + offset3 + (((int32_t)500 * DIR3 * scale3) / 100)), (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    uint16_t test4 = clamp_val((uint16_t)(SERVO_NEUTRAL_US + offset4 + (((int32_t)500 * DIR4 * scale4) / 100)), (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);

    servo1.writeMicroseconds(test1);
    servo2.writeMicroseconds(test2);
    servo3.writeMicroseconds(test3);
    servo4.writeMicroseconds(test4);
    delay(1000);
#endif

    // 停止
    servo1.writeMicroseconds(SERVO_NEUTRAL_US + offset1);
    servo2.writeMicroseconds(SERVO_NEUTRAL_US + offset2);
    servo3.writeMicroseconds(SERVO_NEUTRAL_US + offset3);
    servo4.writeMicroseconds(SERVO_NEUTRAL_US + offset4);

    // I2C初期化
    Wire.swap(1);

    // I2C スレーブ開始 (アドレス: 0x30 / スキャンモード対応)
    Wire.begin(I2C_ADDR_LEG);
    Wire.onReceive(receiveEvent);
    Wire.onRequest(requestEvent);

    last_wd_update_time = millis();
}

void loop() {
    // 現在時刻の取得
    uint32_t now = millis();

    int16_t vx = 0;
    int16_t vy = 0;
    int16_t omega = 0;

    // TWI バスエラーステータスの自動復旧
    if (TWI0.SSTATUS & (TWI_COLL_bm | TWI_BUSERR_bm)) {
        TWI0.SSTATUS |= (TWI_COLL_bm | TWI_BUSERR_bm);
    }

    if (now - last_wd_update_time <= WD_TIMEOUT_MS) {
        // レジスタから Vx, Vy, Omega を原子的に読み出し (Little-Endian)
        noInterrupts();
        vx    = (int16_t)(registers[REG_LEG_VX_L]    | (registers[REG_LEG_VX_H] << 8));
        vy    = (int16_t)(registers[REG_LEG_VY_L]    | (registers[REG_LEG_VY_H] << 8));
        omega = (int16_t)(registers[REG_LEG_OMEGA_L] | (registers[REG_LEG_OMEGA_H] << 8));
        interrupts();

        // 規定スケール範囲 [-1000, 1000] にクランプ
        vx    = clamp_val(vx, (int16_t)-1000, (int16_t)1000);
        vy    = clamp_val(vy, (int16_t)-1000, (int16_t)1000);
        omega = clamp_val(omega, (int16_t)-1000, (int16_t)1000);

        // オフセット・スケールの変更確認
        int16_t current_offset1 = 0;
        int16_t current_offset2 = 0;
        int16_t current_offset3 = 0;
        int16_t current_offset4 = 0;
        uint8_t current_scale1 = 0u;
        uint8_t current_scale2 = 0u;
        uint8_t current_scale3 = 0u;
        uint8_t current_scale4 = 0u;
        uint16_t temp = 0u;
        
        // 値を取得する
        temp = registers[REG_LEG_OFFSET1_L] | ((uint16_t)registers[REG_LEG_OFFSET1_H] << 8);
        current_offset1 = (int16_t)temp;
        temp = registers[REG_LEG_OFFSET2_L] | ((uint16_t)registers[REG_LEG_OFFSET2_H] << 8);
        current_offset2 = (int16_t)temp;
        temp = registers[REG_LEG_OFFSET3_L] | ((uint16_t)registers[REG_LEG_OFFSET3_H] << 8);
        current_offset3 = (int16_t)temp;
        temp = registers[REG_LEG_OFFSET4_L] | ((uint16_t)registers[REG_LEG_OFFSET4_H] << 8);
        current_offset4 = (int16_t)temp;
        current_scale1 = registers[REG_LEG_SCALE1];
        current_scale2 = registers[REG_LEG_SCALE2];
        current_scale3 = registers[REG_LEG_SCALE3];
        current_scale4 = registers[REG_LEG_SCALE4];

        if (current_offset1 != offset1) {
            offset1 = current_offset1;
            eeprom_write_i16(REG_LEG_OFFSET1_L, offset1);
        }
        if (current_offset2 != offset2) {
            offset2 = current_offset2;
            eeprom_write_i16(REG_LEG_OFFSET2_L, offset2);
        }
        if (current_offset3 != offset3) {
            offset3 = current_offset3;
            eeprom_write_i16(REG_LEG_OFFSET3_L, offset3);
        }
        if (current_offset4 != offset4) {
            offset4 = current_offset4;
            eeprom_write_i16(REG_LEG_OFFSET4_L, offset4);
        }
        if (current_scale1 != scale1) {
            scale1 = current_scale1;
            eeprom_write_u8(REG_LEG_SCALE1, scale1);
        }
        if (current_scale2 != scale2) {
            scale2 = current_scale2;
            eeprom_write_u8(REG_LEG_SCALE2, scale2);
        }
        if (current_scale3 != scale3) {
            scale3 = current_scale3;
            eeprom_write_u8(REG_LEG_SCALE3, scale3);
        }
        if (current_scale4 != scale4) {
            scale4 = current_scale4;
            eeprom_write_u8(REG_LEG_SCALE4, scale4);
        }
    } else {
        vx = 0;
        vy = 0;
        omega = 0;
    }

#if DEVICE_ID == 0x01
    //----------------------------------------------------------------------
    // オムニホイールモード
    //----------------------------------------------------------------------
    int32_t speed_fl = (int32_t)( vx + vy + omega);
    int32_t speed_fr = (int32_t)(-vx + vy + omega);
    int32_t speed_rl = (int32_t)( vx - vy + omega);
    int32_t speed_rr = (int32_t)(-vx - vy + omega);

    // 各輪の速度を [-1000, 1000] に制限
    speed_fl = clamp_val(speed_fl, (int32_t)-1000, (int32_t)1000);
    speed_fr = clamp_val(speed_fr, (int32_t)-1000, (int32_t)1000);
    speed_rl = clamp_val(speed_rl, (int32_t)-1000, (int32_t)1000);
    speed_rr = clamp_val(speed_rr, (int32_t)-1000, (int32_t)1000);

    // 速度 [-1000, 1000] を PWM パルス幅 [1000us, 2000us] (ニュートラル 1500us + OFFSET + スケール適用) に換算
    // パルス幅 = 1500 + OFFSET + ((speed * DIR * SCALE) / 100 / 2)
    uint16_t pulse4 = (uint16_t)(SERVO_NEUTRAL_US + offset4 + ((speed_fl * DIR4 * scale4) / 200));
    uint16_t pulse2 = (uint16_t)(SERVO_NEUTRAL_US + offset2 + ((speed_fr * DIR2 * scale2) / 200));
    uint16_t pulse3 = (uint16_t)(SERVO_NEUTRAL_US + offset3 + ((speed_rl * DIR3 * scale3) / 200));
    uint16_t pulse1 = (uint16_t)(SERVO_NEUTRAL_US + offset1 + ((speed_rr * DIR1 * scale1) / 200));

    // パルス幅の安全ガードクランプ [1000, 2000]
    pulse1 = clamp_val(pulse1, (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    pulse2 = clamp_val(pulse2, (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    pulse3 = clamp_val(pulse3, (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    pulse4 = clamp_val(pulse4, (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);

    // PWM信号出力（停止時もニュートラルパルスを出力し続ける）
    servo1.writeMicroseconds(pulse1); // PWM1 (PA4): 右後
    servo2.writeMicroseconds(pulse2); // PWM2 (PA5): 右前
    servo3.writeMicroseconds(pulse3); // PWM3 (PA6): 左後
    servo4.writeMicroseconds(pulse4); // PWM4 (PA7): 左前

#elif DEVICE_ID == 0x02
    //----------------------------------------------------------------------
    // 差動二輪モード (前後移動軸: vx, 旋回軸: omega)
    //  バック走行にも完全対応 (vx < 0 で後退・後退旋回)
    //  PWM1: 右輪 (正回転 DIR=+1), PWM3: 左輪 (逆回転 DIR=-1)
    //----------------------------------------------------------------------
    int32_t speed_r = (int32_t)(vx - omega);
    int32_t speed_l = (int32_t)(vx + omega);

    // 規定スケール範囲 [-1000, 1000] にクランプ
    speed_r = clamp_val(speed_r, (int32_t)-1000, (int32_t)1000);
    speed_l = clamp_val(speed_l, (int32_t)-1000, (int32_t)1000);

    // パルス幅換算 (PWM1: 右輪正方向 DIR=+1, PWM3: 左輪逆方向 DIR=-1)
    uint16_t pulse1 = (uint16_t)(SERVO_NEUTRAL_US + offset1 + ((speed_r * (+1) * scale1) / 200));
    uint16_t pulse3 = (uint16_t)(SERVO_NEUTRAL_US + offset3 + ((speed_l * (-1) * scale3) / 200));

    // パルス幅の安全ガードクランプ [1000, 2000]
    pulse1 = clamp_val(pulse1, (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    pulse3 = clamp_val(pulse3, (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);

    // PWM信号出力 (PWM1: 右輪, PWM3: 左輪, PWM2/PWM4: ニュートラル停止)
    servo1.writeMicroseconds(pulse1);
    servo2.writeMicroseconds(SERVO_NEUTRAL_US + offset2);
    servo3.writeMicroseconds(pulse3);
    servo4.writeMicroseconds(SERVO_NEUTRAL_US + offset4);

#elif DEVICE_ID == 0x03
    //----------------------------------------------------------------------
    // キャタピラモード (前後移動軸: +vx, 旋回軸: omega)
    //  全地形走破性特化・最大デューティ比 65% 制限で安定低速走行
    //  PWM1: 右履帯 (逆回転 DIR=-1), PWM3: 左履帯 (正回転 DIR=+1)
    //----------------------------------------------------------------------
    #define CRAWLER_DUTY_LIMIT_PERCENT 65
    int32_t raw_r = (int32_t)(vx - omega);
    int32_t raw_l = (int32_t)(vx + omega);

    int32_t speed_r = (raw_r * CRAWLER_DUTY_LIMIT_PERCENT) / 100;
    int32_t speed_l = (raw_l * CRAWLER_DUTY_LIMIT_PERCENT) / 100;

    speed_r = clamp_val(speed_r, (int32_t)-1000, (int32_t)1000);
    speed_l = clamp_val(speed_l, (int32_t)-1000, (int32_t)1000);

    // パルス幅換算 (PWM1: 右履帯逆方向 DIR=-1, PWM3: 左履帯正方向 DIR=+1)
    uint16_t pulse1 = (uint16_t)(SERVO_NEUTRAL_US + offset1 + ((speed_r * (-1) * scale1) / 200));
    uint16_t pulse3 = (uint16_t)(SERVO_NEUTRAL_US + offset3 + ((speed_l * (+1) * scale3) / 200));

    pulse1 = clamp_val(pulse1, (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);
    pulse3 = clamp_val(pulse3, (uint16_t)SERVO_MIN_US, (uint16_t)SERVO_MAX_US);

    servo1.writeMicroseconds(pulse1);
    servo2.writeMicroseconds(SERVO_NEUTRAL_US + offset2);
    servo3.writeMicroseconds(pulse3);
    servo4.writeMicroseconds(SERVO_NEUTRAL_US + offset4);

#else

#endif

    delay(10);
}