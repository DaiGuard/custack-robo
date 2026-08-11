#include <Arduino.h>
#include <M5Unified.h>


void setup() {
  // M5Stack Core2 初期化
  M5.begin();

}


void loop() {
  // M5Stack Core2 の更新処理
  M5.update();

  // 現在時間の取得
  uint32_t now = millis();
}

