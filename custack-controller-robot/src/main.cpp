#include <Arduino.h>
#include <M5Unified.h>
#include <WiFi.h>
#include <ROBO_WCOM.h>
#include <controller_packet.h>
#include <robo_packet.h>

#ifndef TARGET_MAC
#define TARGET_MAC "FF:FF:FF:FF:FF:FF"
#endif


// ready MAC address
uint8_t hostMacAddr[6];
uint8_t targetMacAddr[6];

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

    // Initialize ROBO_WCOM library
    ROBO_WCOM::Init(hostMacAddr, targetMacAddr, millis(), 1000);
}

void loop() {
    // just update M5 state
    M5.update();

    char hostMac[20];
    char targetMac[20];

    snprintf(hostMac, 20, "%02X:%02X:%02X:%02X:%02X:%02X",
        hostMacAddr[0], hostMacAddr[1], hostMacAddr[2],
        hostMacAddr[3], hostMacAddr[4], hostMacAddr[5]);
    snprintf(targetMac, 20, "%02X:%02X:%02X:%02X:%02X:%02X",
        targetMacAddr[0], targetMacAddr[1], targetMacAddr[2],
        targetMacAddr[3], targetMacAddr[4], targetMacAddr[5]);

    M5.Lcd.fillScreen(BLACK);
    M5.Lcd.setCursor(0, 0);
    M5.Lcd.setTextColor(WHITE);
    M5.Lcd.setTextSize(2);
    M5.Lcd.print("Host Mac Address: ");
    M5.Lcd.println(hostMac);
    M5.Lcd.print("Target Mac Address: ");
    M5.Lcd.println(targetMac);

    uint32_t rcvTimeStamp;
    uint8_t controllerAddress[6];
    uint8_t rcvSize;
    RoboCommand_t rcvCommand;

    auto ret = ROBO_WCOM::PeekLatestPacket(millis(), 
        &rcvTimeStamp, controllerAddress,
        reinterpret_cast<uint8_t*>(&rcvCommand), &rcvSize);
    if(ret == ROBO_WCOM::Status::Ok)
    {
        M5.Lcd.println("OK");
    }
    else{
        M5.Lcd.println("NG");
    }

    delay(10);
}
