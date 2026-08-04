#include <Servo.h>

// レッグユニット回路図に基づくピン割り当て (PA4 ~ PA7)
const int SERVO_PIN_1 = PIN_PA4; // J9 (PWM1)
const int SERVO_PIN_2 = PIN_PA5; // J10 (PWM2)
const int SERVO_PIN_3 = PIN_PA6; // J11 (PWM3)
const int SERVO_PIN_4 = PIN_PA7; // J12 (PWM4)

Servo servo1;
Servo servo2;
Servo servo3;
Servo servo4;

void setup() {
  pinMode(SERVO_PIN_1, OUTPUT);
  pinMode(SERVO_PIN_2, OUTPUT);
  pinMode(SERVO_PIN_3, OUTPUT);
  pinMode(SERVO_PIN_4, OUTPUT);

  digitalWrite(SERVO_PIN_1, LOW);
  digitalWrite(SERVO_PIN_2, LOW);
  digitalWrite(SERVO_PIN_3, LOW);
  digitalWrite(SERVO_PIN_4, LOW);

  // SG90の標準的なパルス幅: 500us (0度) 〜 2400us (180度)
  servo1.attach(SERVO_PIN_1, 500, 2400);
  servo2.attach(SERVO_PIN_2, 500, 2400);
  servo3.attach(SERVO_PIN_3, 500, 2400);
  servo4.attach(SERVO_PIN_4, 500, 2400);
}

void loop() {
  // 0度から180度へ回転
  for (int angle = 0; angle <= 180; angle += 1) {
    servo1.write(angle);
    servo2.write(angle);
    servo3.write(angle);
    servo4.write(angle);
    delay(15); // 回転速度の調整
  }

  delay(500); // 180度位置で一時停止

  // 180度から0度へ戻る
  for (int angle = 180; angle >= 0; angle -= 1) {
    servo1.write(angle);
    servo2.write(angle);
    servo3.write(angle);
    servo4.write(angle);
    delay(15);
  }

  delay(500); // 0度位置で一時停止
}