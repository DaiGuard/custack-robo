#include <Arduino.h>
#include <M5Unified.h>
#include "robot_status.h"
#include "face_display.h"
#include "espnow_com.h"
#include "unit_ctrl.h"

// ==========================================
// ピン・ハードウェア定義
// ==========================================
static constexpr uint8_t PIN_I2C_SDA   = 32;
static constexpr uint8_t PIN_I2C_SCL   = 33;
static constexpr uint8_t PIN_BAT_ADC   = 34;
static constexpr uint8_t PIN_DCDC_CTRL = 19;

// ==========================================
// バッテリー監視・保護設定
// ==========================================
static constexpr float V_BATT_WARN = 6.3f; // 警告閾値
static constexpr float V_BATT_CRIT = 5.7f; // 遮断閾値
static constexpr float V_USB_MAX = 5.0f; // USB給電時最大
static constexpr float V_USB_MIN = 4.2f; // USB給電時最小


RobotStatus robot_status = {
  .battery_status = 0,
  .batt_value = 0.0f,

  .espnow_status = false,
  .poweron_enable = true,

  .leg_status = false,
  .rarm_status = false,
  .larm_status = false,

  .move_status = false,
  .vx = 0,
  .vy = 0,
  .omega = 0,
  .rarm = 0,
  .larm = 0,
  .watchdog = 0,
};

// 顔表示クラスのインスタンスを作成
FaceDisplay faceDisplay;

// ESP-NOW通信のインスタンスを作成
ESPNowCom espnowCom;

// ユニットコントロール用のインスタンスを作成 
UnitControl unitControl;

/**
 * @brief バックグラウンドタスク
 * 
 * @param params 
 */
void backgroundTask(void* params) {
  // タスクループ
  while(true) {
    // M5Stack Core2 の更新処理
    M5.update();

    // 現在時間の取得
    uint32_t now = millis();

    // ボタン操作を検出
    static bool info_sound = false;
    static bool power_sound = false;
    static bool info_enable = false;
    static bool power_enable = true;
    if (M5.BtnA.wasHold()) {
      info_sound = true;
      info_enable = !info_enable;
    } else if (M5.BtnB.wasHold()) {
      power_sound = true;
      power_enable = !power_enable;
    }

    // サウンドの更新
    if (info_sound) {
      faceDisplay.infoSound(info_enable);
      info_sound = false;
    }
    if (power_sound) {
      faceDisplay.powerSound(power_enable);
      power_sound = false;
    }

    // 5V供給制御
    digitalWrite(PIN_DCDC_CTRL, power_enable ? HIGH : LOW);

    // バッテリ電圧測定
    int raw_adc = analogRead(PIN_BAT_ADC);
    static float filtered_adc = 0.0f;
    filtered_adc = 0.7f * filtered_adc + 0.3f * raw_adc;
    float batt_value = (filtered_adc * 3.3f / 4095.0f) * (15.1f / 5.1f);

    // バッテリステータスの更新
    if(batt_value < V_USB_MAX && batt_value > V_USB_MIN) {
      robot_status.battery_status = 3;
    } else if (batt_value < V_BATT_CRIT) {
      robot_status.battery_status = 2;
    } else if (batt_value < V_BATT_WARN) {
      robot_status.battery_status = 1;
    } else {
      robot_status.battery_status = 0;
    }

    // 動作中検知
    if (robot_status.vx != 0 || robot_status.vy != 0 || robot_status.omega != 0 
        || robot_status.rarm != 0 || robot_status.larm != 0) {
      robot_status.move_status = true;
    } else {
      robot_status.move_status = false;
    }

    // ロボットステータス更新
    robot_status.poweron_enable = power_enable;
    robot_status.batt_value = batt_value;
    robot_status.mac = espnowCom.getOwnMacAddress();

    // 表情の更新
    int face_type = 0;
    if (robot_status.move_status) {
      face_type = 1;
    }
    if (robot_status.battery_status > 1) {
      face_type = 2;
    }
    faceDisplay.update(
      face_type, now, 
      info_enable, &robot_status);

    vTaskDelay(pdMS_TO_TICKS(50));
  }
}

/**
 * @brief セットアップ
 * 
 */
void setup() {
  // M5Stack Core2 初期化
  auto cfg = M5.config();
  cfg.external_spk = true;
  cfg.external_spk = false;
  M5.begin(cfg);

  // ボタンしきい値を表示
  M5.BtnA.setHoldThresh(700);
  M5.BtnB.setHoldThresh(700);
  M5.BtnC.setHoldThresh(700);

  delay(300);

  // 表情描画の初期化
  faceDisplay.begin();

  // ESP-NOW通信の初期化
  if (!espnowCom.begin()) {
    while(true) {
      delay(1000);
    }
  }

  // ユニットコントロールの初期化
  unitControl.begin(PIN_I2C_SDA, PIN_I2C_SCL);

  // バックグラウンドタスクを実行
  xTaskCreatePinnedToCore(
    backgroundTask, "backgroundTask",
    4096, NULL, 1, NULL, 0
  );

  // 5V電源供給設定
  pinMode(PIN_DCDC_CTRL, OUTPUT);

  delay(100);
}

void loop() {
  // 現在時間の取得
  uint32_t now = millis();

  EspNowCommandPacket packet;

  // ESP-NOW通信のデータ更新
  bool espnow_result = espnowCom.update(now, &packet);
  robot_status.espnow_status = espnow_result;
  robot_status.vx = packet.vx;
  robot_status.vy = packet.vy;
  robot_status.omega = packet.omega;
  robot_status.rarm = packet.arm_right;
  robot_status.larm = packet.arm_left;
  robot_status.watchdog = packet.watchdog;

  // ユニットコントロールのデータ更新 
  unitControl.update(now,
    packet.vx, packet.vy, packet.omega,
    packet.arm_right, packet.arm_left,
    packet.watchdog
  );
  robot_status.leg_status = unitControl.legUnitFound();
  robot_status.rarm_status = unitControl.rarmUnitFound();
  robot_status.larm_status = unitControl.larmUnitFound();

  vTaskDelay(1);
}

