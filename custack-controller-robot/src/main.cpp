#include <Arduino.h>
#include <M5Unified.h>
#include <WiFi.h>
#include <ROBO_WCOM.h>
#include <controller_packet.h>
#include <robo_packet.h>

#ifndef DEVICE_ID
#define DEVICE_ID 0
#endif

#if DEVICE_ID < 0 || DEVICE_ID > 6
#error "DEVICE_ID must be between 0 and 6"
#endif

#ifndef TARGET_MAC
#define TARGET_MAC "FF:FF:FF:FF:FF:FF"
#endif

#ifndef DRIVE_TYPE
#define DRIVE_TYPE "Diff2Wheel"
#endif

#ifdef DIFF2WHEEL
#undef OMNIWHEEL
#elif OMNIWHEEL
#undef DIFF2WHEEL
#else
#error "Please define DRIVE_TYPE to either Diff2Wheel or OmniWheel"
#endif


// ready MAC address
uint8_t hostMacAddr[6];
uint8_t targetMacAddr[6];

// mac address string for display
char hostMac[20];
char targetMac[20];

uint32_t lastMillis = 0u;
uint32_t lastDutyMillis = 0u;

int32_t angle = 0;
int32_t dir = 5;
int32_t last_d1 = map(0, -100, 100, 1850, 7800);;
int32_t last_d2 = map(0, -100, 100, 1850, 7800);;
int32_t last_d3 = map(0, -100, 100, 1850, 7800);;
int32_t last_d4 = map(0, -100, 100, 1850, 7800);;

void setup() {
    // Start serial communication for debugging    
    Serial.begin(115200);

    // Initialize the M5Stack
    auto cfg = M5.config();
    M5.begin(cfg);
    M5.Power.begin();

    // get host mac address
    WiFi.macAddress(hostMacAddr);
    // parse target mac address
    sscanf(TARGET_MAC, "%hhx:%hhx:%hhx:%hhx:%hhx:%hhx",
        &targetMacAddr[0], &targetMacAddr[1], &targetMacAddr[2], 
        &targetMacAddr[3], &targetMacAddr[4], &targetMacAddr[5]);

    // set mac address string
    snprintf(hostMac, 20, "%02X:%02X:%02X:%02X:%02X:%02X",
        hostMacAddr[0], hostMacAddr[1], hostMacAddr[2],
        hostMacAddr[3], hostMacAddr[4], hostMacAddr[5]);
    snprintf(targetMac, 20, "%02X:%02X:%02X:%02X:%02X:%02X",
        targetMacAddr[0], targetMacAddr[1], targetMacAddr[2],
        targetMacAddr[3], targetMacAddr[4], targetMacAddr[5]);

    // Initialize ROBO_WCOM library
    ROBO_WCOM::Init(hostMacAddr, targetMacAddr, millis(), 1000);

    // Servo control pin set up
    #ifdef DIFF2WHEEL
    int duty_cycle = map(0, -100, 100, 1800, 7800);
    ledcSetup(0, 50, 16);
    ledcAttachPin(27, 0);
    ledcSetup(1, 50, 16);
    ledcAttachPin(2, 1);
    ledcWrite(0, duty_cycle);
    delay(800);
    ledcWrite(1, duty_cycle);
    delay(800);
    #elif OMNIWHEEL
    int duty_cycle = map(0, -100, 100, 1850, 7800);
    ledcSetup(0, 50, 16);
    ledcAttachPin(19, 0);
    ledcSetup(1, 50, 16);
    ledcAttachPin(33, 1);
    ledcSetup(2, 50, 16);
    ledcAttachPin(14, 2);
    ledcSetup(3, 50, 16);
    ledcAttachPin(27, 3);
    ledcWrite(0, duty_cycle);
    delay(800);
    ledcWrite(1, duty_cycle);
    delay(800);
    ledcWrite(2, duty_cycle);
    delay(800);
    ledcWrite(3, duty_cycle);
    delay(800);
    #endif
}

void loop() {
    // just update M5 state
    M5.update();

    // get current time
    uint32_t t = millis();

    M5.Lcd.fillScreen(BLACK);
    M5.Lcd.setCursor(0, 0);
    M5.Lcd.setTextColor(WHITE);
    M5.Lcd.setTextSize(2);
    M5.Lcd.print("ID: ");
    M5.Lcd.println(DEVICE_ID);
    M5.Lcd.print("Host Mac Address: ");
    M5.Lcd.println(hostMac);
    M5.Lcd.print("Target Mac Address: ");
    M5.Lcd.println(targetMac);

    uint32_t rcvTimeStamp;
    uint8_t controllerAddress[6];
    uint8_t rcvSize;
    RoboCommand_t rcvCommand;

    // recv data from host
    auto ret = ROBO_WCOM::PeekLatestPacket(t, 
        &rcvTimeStamp, controllerAddress,
        reinterpret_cast<uint8_t*>(&rcvCommand), &rcvSize);
    M5.Lcd.print("BAT: "); M5.Lcd.println(M5.Power.getBatteryLevel());
    if(ret == ROBO_WCOM::Status::Ok)
    {
        M5.Lcd.println("OK");
        M5.Lcd.print("X: ");
        M5.Lcd.println(rcvCommand.velocity.x);
        M5.Lcd.print("Y: ");
        M5.Lcd.println(rcvCommand.velocity.y);
        M5.Lcd.print("W: ");
        M5.Lcd.println(rcvCommand.velocity.omega);
    }
    else{
        M5.Lcd.print("NG: "); M5.Lcd.println((int8_t)ret);
    }

    // send data to host
    if(t - lastMillis > 500) {
        RoboStatus_t sendStatus;
        ROBO_WCOM::SendPacket(t,
            reinterpret_cast<uint8_t*>(&sendStatus), sizeof(RoboCommand_t));

        lastMillis = t;
    }

    // Servo control
    float x = rcvCommand.velocity.x;
    float y = rcvCommand.velocity.y;
    float omega = rcvCommand.velocity.omega;
    // スケーリングとクランプ
    x = constrain(x, -1.0f, 1.0f);
    y = constrain(y, -1.0f, 1.0f);
    omega = constrain(omega, -1.0f, 1.0f);

    #ifdef DIFF2WHEEL
    int w1 = (y * 0.5f - x * 0.3f) * -100.0f;
    int w2 = (y * 0.5f + x * 0.3f) *  100.0f;
    int d1 = map(w1, -100, 100, 1900, 7800);
    int d2 = map(w2, -100, 100, 1800, 7800);
    M5.Lcd.print("D1: "); M5.Lcd.println(d1);
    M5.Lcd.print("D2: "); M5.Lcd.println(d2);
    ledcWrite(0, d1);
    ledcWrite(1, d2);
    #elif OMNIWHEEL
    // 各ホイールの速度計算 (仮の係数)
    float w1 = (-x - y + omega);
    float w2 = ( x - y + omega);
    float w3 = ( x + y + omega);
    float w4 = (-x + y + omega);
    w1 = constrain(w1, -1.0f, 1.0f) * 100.0f;
    w2 = constrain(w2, -1.0f, 1.0f) * 100.0f;
    w3 = constrain(w3, -1.0f, 1.0f) * 100.0f;
    w4 = constrain(w4, -1.0f, 1.0f) * 100.0f;
    int d1 = map(w1, -100, 100, 1850, 7800);
    int d2 = map(w2, -100, 100, 1850, 7800);
    int d3 = map(w3, -100, 100, 1850, 7800);
    int d4 = map(w4, -100, 100, 1850, 7800);
    d1 = int(d1 * 0.3 + last_d1 * 0.7);
    d2 = int(d2 * 0.3 + last_d2 * 0.7);
    d3 = int(d3 * 0.3 + last_d3 * 0.7);
    d4 = int(d4 * 0.3 + last_d4 * 0.7);
    last_d1 = d1;
    last_d2 = d2;
    last_d3 = d3;
    last_d4 = d4;
    M5.Lcd.print("D1: "); M5.Lcd.println(d1);
    M5.Lcd.print("D2: "); M5.Lcd.println(d2);
    M5.Lcd.print("D3: "); M5.Lcd.println(d3);
    M5.Lcd.print("D4: "); M5.Lcd.println(d4);
    ledcWrite(0, d1);
    ledcWrite(1, d2);
    ledcWrite(2, d3);
    ledcWrite(3, d4);
    #endif

    delay(1);
}
