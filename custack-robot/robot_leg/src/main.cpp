#include <Arduino.h>
#include <Wire.h>
#include <Servo.h>

// I2C Slave Address
#define I2C_SLAVE_ADDR 0x30

// Pin Definitions for ATtiny1614 (Leg Unit PCB)
#define PIN_PWM1 PIN_PA4 // 左後 (Rear-Left)
#define PIN_PWM2 PIN_PA5 // 左前 (Front-Left)
#define PIN_PWM3 PIN_PA6 // 右後 (Rear-Right)
#define PIN_PWM4 PIN_PA7 // 右前 (Front-Right)

// Servo instances
Servo servo_rl; // PWM1: 左後
Servo servo_fl; // PWM2: 左前
Servo servo_rr; // PWM3: 右後
Servo servo_fr; // PWM4: 右前

// Register Map Size
#define REG_MAP_SIZE 8

// Register Map Addresses
#define REG_DEVICE_ID 0x00 // R: デバイス識別番号 (仮値 0x01)
#define REG_WATCHDOG  0x01 // W: ウォッチドッグ
#define REG_VX_L      0x02 // W: 移動速度 Vx Low Byte
#define REG_VX_H      0x03 // W: 移動速度 Vx High Byte
#define REG_VY_L      0x04 // W: 移動速度 Vy Low Byte
#define REG_VY_H      0x05 // W: 移動速度 Vy High Byte
#define REG_OMEGA_L   0x06 // W: 移動速度 Omega Low Byte
#define REG_OMEGA_H   0x07 // W: 移動速度 Omega High Byte

// Register storage
volatile uint8_t registers[REG_MAP_SIZE] = {0};
volatile uint8_t current_reg_addr = 0;

// Watchdog tracking variables
volatile uint32_t last_wd_update_time = 0;
volatile uint8_t last_wd_value = 0;
#define WD_TIMEOUT_MS 500

// Servo pulse bounds (microseconds)
#define SERVO_NEUTRAL_US 1500
#define SERVO_MIN_US     1000
#define SERVO_MAX_US     2000

// Value clamp utility
template <typename T>
T clamp_val(T val, T min_val, T max_val) {
    if (val < min_val) return min_val;
    if (val > max_val) return max_val;
    return val;
}

// I2C Receive Handler (Master Write)
void receiveEvent(int howMany) {
    if (howMany < 1) return;

    // 先頭バイトは書き込み先レジスタアドレス
    current_reg_addr = Wire.read();
    howMany--;

    // 2バイト目以降は連続データ書き込み (Auto-Increment)
    uint8_t addr = current_reg_addr;
    while (howMany > 0 && Wire.available()) {
        uint8_t val = Wire.read();
        if (addr < REG_MAP_SIZE) {
            if (addr == REG_WATCHDOG) {
                // マスターからのWD更新判定
                if (val != last_wd_value) {
                    last_wd_value = val;
                    last_wd_update_time = millis();
                }
            }
            registers[addr] = val;
            addr++;
        }
        howMany--;
    }
}

// I2C Request Handler (Master Read)
void requestEvent() {
    if (current_reg_addr < REG_MAP_SIZE) {
        Wire.write(registers[current_reg_addr]);
        current_reg_addr++;
    } else {
        Wire.write(0x00);
    }
}

void setup() {
    // レジスタ初期化
    registers[REG_DEVICE_ID] = 0x01; // デバイス識別番号の仮値 0x01
    registers[REG_WATCHDOG]  = 0x00;

    // サーボピンの初期化とアタッチ
    servo_rl.attach(PIN_PWM1, SERVO_MIN_US, SERVO_MAX_US);
    servo_fl.attach(PIN_PWM2, SERVO_MIN_US, SERVO_MAX_US);
    servo_rr.attach(PIN_PWM3, SERVO_MIN_US, SERVO_MAX_US);
    servo_fr.attach(PIN_PWM4, SERVO_MIN_US, SERVO_MAX_US);

    // 初期状態: 全輪停止パルス (1500us) を出力
    servo_rl.writeMicroseconds(SERVO_NEUTRAL_US);
    servo_fl.writeMicroseconds(SERVO_NEUTRAL_US);
    servo_rr.writeMicroseconds(SERVO_NEUTRAL_US);
    servo_fr.writeMicroseconds(SERVO_NEUTRAL_US);

    // I2C スレーブ開始 (アドレス 0x30)
    Wire.begin(I2C_SLAVE_ADDR);
    Wire.onReceive(receiveEvent);
    Wire.onRequest(requestEvent);

    last_wd_update_time = millis();
}

void loop() {
    int16_t vx = 0;
    int16_t vy = 0;
    int16_t omega = 0;

    // ウォッチドッグのタイムアウトチェック
    bool is_wd_valid = (millis() - last_wd_update_time) <= WD_TIMEOUT_MS;

    if (is_wd_valid) {
        // レジスタから Vx, Vy, Omega を取得 (Little-Endian)
        noInterrupts();
        vx    = (int16_t)(registers[REG_VX_L]    | (registers[REG_VX_H] << 8));
        vy    = (int16_t)(registers[REG_VY_L]    | (registers[REG_VY_H] << 8));
        omega = (int16_t)(registers[REG_OMEGA_L] | (registers[REG_OMEGA_H] << 8));
        interrupts();

        // 期待範囲 [-1000, 1000] にクランプ
        vx    = clamp_val(vx, (int16_t)-1000, (int16_t)1000);
        vy    = clamp_val(vy, (int16_t)-1000, (int16_t)1000);
        omega = clamp_val(omega, (int16_t)-1000, (int16_t)1000);
    } else {
        // WDタイムアウト時は安全のため速度ゼロ (停止)
        vx = 0;
        vy = 0;
        omega = 0;
    }

    // 4輪オムニホイールの運動学計算 (Kinematics)
    // FL (PWM2): -Vx + Vy + Omega
    // FR (PWM4):  Vx + Vy + Omega
    // RL (PWM1): -Vx - Vy + Omega
    // RR (PWM3):  Vx - Vy + Omega
    int32_t speed_fl = -vx + vy + omega;
    int32_t speed_fr =  vx + vy + omega;
    int32_t speed_rl = -vx - vy + omega;
    int32_t speed_rr =  vx - vy + omega;

    // 各輪速度を [-1000, 1000] にクランプ
    speed_fl = clamp_val(speed_fl, (int32_t)-1000, (int32_t)1000);
    speed_fr = clamp_val(speed_fr, (int32_t)-1000, (int32_t)1000);
    speed_rl = clamp_val(speed_rl, (int32_t)-1000, (int32_t)1000);
    speed_rr = clamp_val(speed_rr, (int32_t)-1000, (int32_t)1000);

    // 速度 [-1000, 1000] をサーボパルス幅 [1000us, 2000us] (ニュートラル 1500us) に換算
    uint16_t pulse_fl = SERVO_NEUTRAL_US + (speed_fl / 2);
    uint16_t pulse_fr = SERVO_NEUTRAL_US + (speed_fr / 2);
    uint16_t pulse_rl = SERVO_NEUTRAL_US + (speed_rl / 2);
    uint16_t pulse_rr = SERVO_NEUTRAL_US + (speed_rr / 2);

    // PWM パルス信号出力
    servo_fl.writeMicroseconds(pulse_fl); // PWM2 (PA5)
    servo_fr.writeMicroseconds(pulse_fr); // PWM4 (PA7)
    servo_rl.writeMicroseconds(pulse_rl); // PWM1 (PA4)
    servo_rr.writeMicroseconds(pulse_rr); // PWM3 (PA6)

    delay(10); // 100Hz 制御周期
}