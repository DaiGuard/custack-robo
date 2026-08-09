#include <Arduino.h>

// PWM設定パラメータ
#define PWM_FREQ_HZ 20000   // 20kHz (20,000 Hz)
#define DEFAULT_DUTY 20     // デューティ比 20%

/**
 * @brief PA4 (物理ピン2) に 20kHz PWM を出力する関数 (TCA0 Split Mode 使用)
 * 
 * @param frequency_hz PWM周波数 [Hz] (20000)
 * @param duty_percent デューティ比 [0 ~ 100%]
 */
void initPWM_PA4(uint32_t frequency_hz, uint8_t duty_percent) {
    if (duty_percent > 100) duty_percent = 100;

    // PA4 ピンを出力モードに設定
    pinMode(PIN_PA4, OUTPUT);

    // megaTinyCore のタイマー管理から TCA0 を解放・リセット
    takeOverTCA0();

    // TCA0 を Split Mode (8ビットタイマー分割モード) に設定
    TCA0.SPLIT.CTRLD = TCA_SPLIT_SPLITM_bm;

    // 周波数の計算 (プリスケーラ DIV8 を使用)
    // f_PWM = F_CPU / (8 * (HPER + 1))
    // HPER = (F_CPU / (8 * f_PWM)) - 1
    uint8_t hper = (uint8_t)((F_CPU / (8UL * frequency_hz)) - 1);
    TCA0.SPLIT.HPER = hper;
    TCA0.SPLIT.LPER = hper;

    // デューティ比から HCMP1 (WO4 = PA4) の比較値を計算
    uint8_t hcmp1 = (uint8_t)(((uint16_t)(hper + 1) * duty_percent) / 100);
    TCA0.SPLIT.HCMP1 = hcmp1;

    // WO4 (PA4) の PWM 出力を有効化
    TCA0.SPLIT.CTRLB = TCA_SPLIT_HCMP1EN_bm;

    // プリスケーラ DIV8 で TCA0 を有効化
    TCA0.SPLIT.CTRLA = TCA_SPLIT_CLKSEL_DIV8_gc | TCA_SPLIT_ENABLE_bm;
}

/**
 * @brief 動的にデューティ比を変更する関数
 * 
 * @param duty_percent デューティ比 [0 ~ 100%]
 */
void setDutyCycle(uint8_t duty_percent) {
    if (duty_percent > 100) duty_percent = 100;
    uint8_t hper = TCA0.SPLIT.HPER;
    uint8_t hcmp1 = (uint8_t)(((uint16_t)(hper + 1) * duty_percent) / 100);
    TCA0.SPLIT.HCMP1 = hcmp1;
}

void setup() {
    // PA4 (ピン2) に 20kHz, デューティ比 20% の PWM を開始
    initPWM_PA4(PWM_FREQ_HZ, DEFAULT_DUTY);
}

void loop() {
    // メインループ
}


