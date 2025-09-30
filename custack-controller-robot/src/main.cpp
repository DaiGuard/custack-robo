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


// ready MAC address
uint8_t hostMacAddr[6];
uint8_t targetMacAddr[6];

// mac address string for display
char hostMac[20];
char targetMac[20];

uint32_t lastMillis = 0u;

void setup() {
    // Start serial communication for debugging    
    Serial.begin(115200);

    // Initialize the M5Stack
    auto cfg = M5.config();
    M5.begin(cfg);

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
    ledcSetup(0, 50, 16);
    ledcAttachPin(27, 0);
    ledcSetup(1, 50, 16);
    ledcAttachPin(2, 1);
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
    int angle = t % 1800 / 10;
    int duty_cycle = map(angle, 0, 180, 1700, 7800);
    ledcWrite(0, duty_cycle);
    ledcWrite(1, duty_cycle);

    delay(10);
}
