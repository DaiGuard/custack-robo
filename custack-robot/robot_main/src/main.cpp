#include <Arduino.h>
#include <M5Unified.h>
#include "espnow_protocol.h"
#include "espnow_com.h"
#include "unit_ctrl.h"
#include "face_display.h"
#include "robot_status.h"

#define PIN_I2C_SDA     32
#define PIN_I2C_SCL     33

#define PIN_BAT_ADC     34
#define PIN_DCDC_CTRL   19

#define V_BATT_WARN     6.3f
#define V_BATT_CRIT     5.7f
#define V_USB_MAX       5.0f
#define V_USB_MIN       4.0f

RobotStatus robot_status;
ESPNowCom espnowCom;
FaceDisplay faceDisplay;
UnitControl unitControl;

void backgroundTask(void *pvParameters) {
  bool info_sound = false;
  bool power_sound = false;

  bool info_enable = false;
  bool power_enable = true;

  while(true) {
    uint32_t now = millis();
    M5.update();

    // ボタンAが長押しされた場合
    if (M5.BtnA.wasHold()) {
      info_sound = true;
      info_enable = !info_enable;
    }

    // ボタンBが長押しされた場合
    if (M5.BtnB.wasHold()) {
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
    robot_status.leg_status = unitControl.legUnitFound();
    robot_status.rarm_status = unitControl.rarmUnitFound();
    robot_status.larm_status = unitControl.larmUnitFound();
    robot_status.leg_id = unitControl.getLegId();
    robot_status.rarm_id = unitControl.getRightArmId();
    robot_status.larm_id = unitControl.getLeftArmId();

    // バッテリークリティカル (5.7V未満) 警告音 (3秒周期)
    static uint32_t last_crit_beep_ms = 0;
    if (robot_status.battery_status == 2) {
      if (now - last_crit_beep_ms > 3000) {
        last_crit_beep_ms = now;
        faceDisplay.warningSound();
      }
    }

    // 表情の更新 (0: 待機, 1: 移動・戦闘, 2: 困り顔/低電圧)
    int face_type = 0;
    if (robot_status.move_status) {
      face_type = 1;
    }
    if (robot_status.battery_status == 2) {
      face_type = 2; // 5.7V未満で困り顔
    }
    faceDisplay.update(
      face_type, now, 
      info_enable, &robot_status);

    vTaskDelay(pdMS_TO_TICKS(50));
  }
}

void setup() {
  // M5Stack Core2 初期化
  auto cfg = M5.config();
  cfg.external_spk = false;
  M5.begin(cfg);

  // ボタンしきい値を設定
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
  robot_status.leg_id = unitControl.getLegId();
  robot_status.rarm_id = unitControl.getRightArmId();
  robot_status.larm_id = unitControl.getLeftArmId();

  // テレメトリ定期送信 (500ms周期)
  static uint32_t last_telemetry_ms = 0;
  static uint8_t tlm_watchdog = 0;
  if (now - last_telemetry_ms >= 500) {
    last_telemetry_ms = now;

    uint8_t flags = 0;
    if (robot_status.poweron_enable) flags |= (1 << 0);
    if (robot_status.battery_status == 1) flags |= (1 << 1);
    if (robot_status.battery_status >= 2) flags |= (1 << 2);

    uint16_t batt_mv = (uint16_t)(robot_status.batt_value * 1000.0f);

    EspNowTelemetryPacket tlm = {
      .leg_id = unitControl.getLegId(),
      .arm_right_id = unitControl.getRightArmId(),
      .arm_left_id = unitControl.getLeftArmId(),
      .status_flags = flags,
      .battery_mv = batt_mv,
      .reserved = {0},
      .watchdog = ++tlm_watchdog
    };

    espnowCom.sendTelemetry(&tlm);
  }

  vTaskDelay(1);
}
