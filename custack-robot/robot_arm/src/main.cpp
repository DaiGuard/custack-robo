#include <Arduino.h>
#include <Wire.h>
#include <Servo.h>
#include <EEPROM.h>
#include "i2c_protocol.h"

// -----------------------------------------------------------------------------
// ハードウェア・ピン定義 (ATtiny1614)
// -----------------------------------------------------------------------------
#define PIN_PWM PIN_PA4

// -----------------------------------------------------------------------------
// PWM設定パラメータ
// -----------------------------------------------------------------------------
#define PWM_FREQ_HZ 20000   // 20kHz (20,000 Hz)
#define DEFAULT_DUTY 20     // デューティ比 20%

// 20% 速度パルス幅 (1600us相当: 速度 +200)
#define TEST_DUTY   15

// -----------------------------------------------------------------------------
// レジスタマップ
// -----------------------------------------------------------------------------
static volatile uint8_t registers[REG_ARM_MAPSIZE] = {0};
static volatile uint8_t current_reg_addr = 0;

// -----------------------------------------------------------------------------
// 変数
// -----------------------------------------------------------------------------
static volatile uint32_t last_wd_update_time = 0;
static volatile uint8_t last_wd_value = 0;

// -----------------------------------------------------------------------------
// 定数設定
// -----------------------------------------------------------------------------
#define WD_TIMEOUT_MS    1000  // ウォッチドックタイムアウト時間


/**
 * @brief PA4 (物理ピン2) に 20kHz PWM を出力する関数 (TCA0 Split Mode 使用)
 * 
 * @param frequency_hz PWM周波数 [Hz] (20000)
 * @param duty_percent デューティ比 [0 ~ 100%]
 */
void initPWM(uint32_t frequency_hz) {
    pinMode(PIN_PWM, OUTPUT);
    digitalWrite(PIN_PWM, LOW);

    // megaTinyCoreの標準タイマー管理からTCA0を解放
    takeOverTCA0();

    // TCA0をSplit Mode (8ビット分割モード)に設定
    TCA0.SPLIT.CTRLD = TCA_SPLIT_SPLITM_bm;

    // 周波数の計算 (プリスケーラ DIV8)
    // f_PWM = F_CPU / (8 * (HPER + 1))  ->  HPER = (F_CPU / (8 * f_PWM)) - 1
    // 20MHz / (8 * 20000) - 1 = 124
    uint8_t hper = (uint8_t)((F_CPU / (8UL * frequency_hz)) - 1);
    TCA0.SPLIT.HPER = hper;
    TCA0.SPLIT.LPER = hper;

    // 初期Duty比 0%
    TCA0.SPLIT.HCMP1 = 0;

    // WO4 (PA4) の PWM 出力を有効化
    TCA0.SPLIT.CTRLB = TCA_SPLIT_HCMP1EN_bm;

    // プリスケーラ DIV8 で TCA0 を有効化
    TCA0.SPLIT.CTRLA = TCA_SPLIT_CLKSEL_DIV8_gc | TCA_SPLIT_ENABLE_bm;
}

/**
 * @brief モータへ適用するDuty比を設定 (0〜255入力 -> 60%クランプ処理)
 * 
 * @param duty 0〜255 (内部で ARM_DUTY_MAX(=153) に自動クランプ)
 */
static void setMotorDuty(uint8_t duty) {
    // 60%上限クランプ処理 (最大 153)
    if (duty > ARM_DUTY_MAX) {
        duty = ARM_DUTY_MAX;
    }

    uint8_t hper = TCA0.SPLIT.HPER;
    // Duty 255 相当を HPER+1 全体に換算して比較値を算出
    uint8_t hcmp1 = (uint8_t)(((uint16_t)(hper + 1) * duty) / 255);
    TCA0.SPLIT.HCMP1 = hcmp1;
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
        if (addr < REG_ARM_MAPSIZE) {
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
    if (current_reg_addr < REG_ARM_MAPSIZE) {
        Wire.write(registers[current_reg_addr]);
        current_reg_addr++;
    } else {
        Wire.write(0x00);
    }
}


void setup() {
    // PA4 (ピン2) に 20kHz, デューティ比 20% の PWM を開始
    initPWM(PWM_FREQ_HZ);

    // レジスタ初期値設定
    registers[REG_DEVICE_ID] = DEVICE_ID;
    registers[REG_FW_VERSION] = FW_VERSION;
    registers[REG_ARM_DUTY] = 0;
    registers[REG_ARM_PATTERN] = ARM_PATTERN_MANUAL;

    // テスト運転
    delay(500);
    setMotorDuty(15);
    delay(500);
    setMotorDuty(0);

    // I2C初期化
    Wire.swap(1);

    // I2C スレーブ開始
    // (右アームアドレス: 0x31 / 左アームアドレス: 0x32 スキャンモード対応)
    Wire.begin(I2C_SLAVE_ADDR);
    Wire.onReceive(receiveEvent);
    Wire.onRequest(requestEvent);

    // ウォッチドックタイマの更新
    last_wd_update_time = millis();
}

void loop() {
    // 現在時刻の取得
    uint32_t now = millis();

    uint8_t duty = 0u;
    uint8_t pattern = 0u;

    // TWI バスエラーステータスの自動復旧
    if (TWI0.SSTATUS & (TWI_COLL_bm | TWI_BUSERR_bm)) {
        TWI0.SSTATUS |= (TWI_COLL_bm | TWI_BUSERR_bm);
    }

    if (now - last_wd_update_time <= WD_TIMEOUT_MS) {
        noInterrupts();
        duty    = registers[REG_ARM_DUTY];
        pattern = registers[REG_ARM_PATTERN];
        interrupts();
    } else {
        duty = 0u;
        pattern = 0u;
    }

#if DEVICE_ID == 0x01
    //----------------------------------------------------------------------
    // マニュアルモード
    //----------------------------------------------------------------------
    static uint8_t last_duty = 0u;
    static uint32_t last_shot_ms = 0u;
    switch(pattern) {
        case ARM_PATTERN_MANUAL:
            setMotorDuty(duty);
            break;
        case ARM_PATTERN_PULSE:
            if (last_duty != duty && duty > last_duty) {
                last_shot_ms = now;
            }
            if (now - last_shot_ms < 100) {
                setMotorDuty(duty);
            } else {
                setMotorDuty(0);
            }
            break;
        case ARM_PATTERN_BLINK:
            if (duty > 0) {
                if ((now % 200) > 150) {
                    setMotorDuty(0);
                } else {
                    setMotorDuty(duty);
                }
            } else {
                setMotorDuty(0);
            }
            break;
        default:
            setMotorDuty(duty);
            break;
    }
    last_duty = duty;

#elif DEVICE_ID == 0x02
    //----------------------------------------------------------------------
    // パルスモード
    //----------------------------------------------------------------------
    static uint8_t last_duty = 0u;
    static uint32_t last_shot_ms = 0u;
    if (last_duty != duty && duty > last_duty) {
        last_shot_ms = now;
    }
    if (now - last_shot_ms < 100) {
        setMotorDuty(duty);
    } else {
        setMotorDuty(0);
    }
    last_duty = duty;

#elif DEVICE_ID == 0x03
    //----------------------------------------------------------------------
    // 点滅モード
    //----------------------------------------------------------------------
    if (duty > 0) {
        if ((now % 200) > 150) {
            setMotorDuty(0);
        } else {
            setMotorDuty(duty);
        }
    } else {
        setMotorDuty(0);
    }

#endif

    delay(10);
}


