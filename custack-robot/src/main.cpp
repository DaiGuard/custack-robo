#include <M5Unified.h>

// Pin definitions (use GPIO numbers as on the board)
static constexpr uint8_t PIN_DCDC_CTRL   = 19; // G19: DC/DC ON/OFF control
static constexpr uint8_t PIN_DCDC_STATUS = 27; // G27: DC/DC status (input)
static constexpr uint8_t PIN_BAT_ADC     = 34; // G34: battery sense (ADC)

// ADC / voltage divider settings - adjust VOLTAGE_DIVIDER to your resistor ratio
static constexpr float ADC_REF = 3.3f;
static constexpr int ADC_MAX = 4095; // 12-bit ADC
static constexpr float VOLTAGE_DIVIDER = 8.4f/2.837f; // battery = measured_voltage * VOLTAGE_DIVIDER

// UI layout
static constexpr int BUTTON_H = 60;

bool dcdcOn = false;

void drawFace()
{
  auto &lcd = M5.Display;
  lcd.fillScreen(TFT_BLACK);

  int w = lcd.width();
  int h = lcd.height();

  // Head
  int cx = w/2;
  int cy = h/2 - 10;
  int r = min(w, h)/3;

  // Eyes
  int ex = 60;
  int ey = 20;
  int eyeY = cy - ey;
  lcd.fillCircle(cx - ex, eyeY, 10, TFT_WHITE);
  lcd.fillCircle(cx + ex, eyeY, 10, TFT_WHITE);
  lcd.fillRoundRect(
    cx-50, eyeY + 40, 100, 12, 6, TFT_WHITE);

  // Draw bottom button area
  int by = h - BUTTON_H;
  lcd.setTextColor(TFT_WHITE, TFT_BLACK);
  lcd.setTextSize(2);
  lcd.setCursor(10, by + 12);
  lcd.print("DC/DC: ");
  lcd.print(dcdcOn ? "ON" : "OFF");
  lcd.setCursor(w - 110, by + 12);
  lcd.print("Toggle");

  // Battery placeholder at top-left
  lcd.setTextSize(2);
  lcd.setTextColor(TFT_WHITE, TFT_BLACK);
  lcd.setCursor(4, 4);
  lcd.print("Bat: --.-V");

  // DC/DC status indicator top-right
  lcd.setCursor(w - 100, 4);
  lcd.print("STAT: ?");
}

void updateBatteryDisplay(float voltage)
{
  auto &lcd = M5.Display;
  char buf[16];
  snprintf(buf, sizeof(buf), "Bat: %.2fV", voltage);
  // clear area
  
  lcd.fillRect(0, 0, 120, 24, TFT_BLACK);
  lcd.setTextSize(2);
  lcd.setTextColor(TFT_WHITE, TFT_BLACK);
  lcd.setCursor(4, 4);
  lcd.print(buf);
}

void updateStatusDisplay(bool status)
{
  auto &lcd = M5.Display;
  lcd.fillRect(lcd.width() - 96, 0, 96, 24, TFT_BLACK);
  lcd.setTextSize(2);
  lcd.setTextColor(TFT_WHITE, TFT_BLACK);
  lcd.setCursor(lcd.width() - 100, 4);
  lcd.print("STAT: ");
  lcd.print(status ? "OK" : "NO");
}

bool pointInBottomButton(int x, int y)
{
  auto &lcd = M5.Display;
  return (y >= lcd.height() - BUTTON_H);
}

void setup() {
  M5.begin();

  pinMode(PIN_DCDC_CTRL, OUTPUT);
  pinMode(PIN_DCDC_STATUS, INPUT);
  // Ensure DC/DC is off at startup
  digitalWrite(PIN_DCDC_CTRL, LOW);
  dcdcOn = false;

  // ADC pin doesn't require pinMode for analogRead on ESP32

  // Display initial UI
  drawFace();
  updateStatusDisplay(digitalRead(PIN_DCDC_STATUS));
}

void loop() {
  static unsigned long lastBatMs = 0;
  const unsigned long BAT_UPDATE_MS = 1000;

  M5.update();

  // Touch handling: look for clicks in bottom area to toggle DC/DC
  if (M5.Touch.getCount()) {
    auto td = M5.Touch.getDetail(0);
    if (td.wasClicked()) {
      if (pointInBottomButton(td.base.x, td.base.y)) {
        dcdcOn = !dcdcOn;
        digitalWrite(PIN_DCDC_CTRL, dcdcOn ? HIGH : LOW);
        // update button text
        int by = M5.Display.height() - BUTTON_H;
        M5.Display.fillRect(0, by, M5.Display.width(), BUTTON_H, TFT_BLACK);
        M5.Display.setTextSize(2);
        M5.Display.setTextColor(TFT_WHITE, TFT_BLACK);
        M5.Display.setCursor(10, by + 12);
        M5.Display.print("DC/DC: ");
        M5.Display.print(dcdcOn ? "ON" : "OFF");
        M5.Display.setCursor(M5.Display.width() - 110, by + 12);
        M5.Display.print("Toggle");
      }
    }
  }

  // Update status indicator
  bool status = digitalRead(PIN_DCDC_STATUS);
  updateStatusDisplay(status);

  // Periodic battery measurement
  if (millis() - lastBatMs >= BAT_UPDATE_MS) {
    lastBatMs = millis();
    int raw = analogReadMilliVolts(PIN_BAT_ADC);
    // float measured = (raw * (ADC_REF / ADC_MAX));
    // float battery = measured * VOLTAGE_DIVIDER;
    float battery = raw * VOLTAGE_DIVIDER / 1000.0f;
    updateBatteryDisplay(battery);
  }

  delay(20);
}
