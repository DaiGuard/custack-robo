#include <Arduino.h>
#include <M5Atom.h>
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


int colorPallet[7][3] = {
    {255, 255, 255},  // 0: White
    {254,  50, 130},  // 1: Electrik Pink
    {255, 119,  39},  // 2: Princeton Orange
    {254, 189,  85},  // 3: Maximum Yellow Red
    {251, 251, 156},  // 4: Canary
    { 64, 205, 234},  // 5: Picton Blue
    { 96,  97, 171}   // 6: Liberty
};

int numberMask[7][5][5] = {
    // 0
    {{0, 1, 1, 1, 0}, {1, 0, 0, 1, 1}, {1, 0, 1, 0, 1}, {1, 1, 0, 0, 1}, {0, 1, 1, 1, 0}},
    // 1
    {{0, 0, 1, 0, 0}, {0, 1, 1, 0, 0}, {0, 0, 1, 0, 0}, {0, 0, 1, 0, 0}, {0, 1, 1, 1, 0}},
    // 2
    {{0, 1, 1, 1, 0}, {1, 0, 0, 0, 1}, {0, 0, 0, 1, 0}, {0, 1, 0, 0, 0}, {1, 1, 1, 1, 1}},
    // 3
    {{0, 1, 1, 1, 0}, {1, 0, 0, 0, 1}, {0, 0, 1, 1, 0}, {1, 0, 0, 0, 1}, {0, 1, 1, 1, 0}},
    // 4
    {{1, 0, 0, 1, 0}, {1, 0, 0, 1, 0}, {1, 1, 1, 1, 1}, {0, 0, 0, 1, 0}, {0, 0, 0, 1 ,0}},
    //5
    {{1 ,1 ,1 ,1 ,1}, {1 ,0 ,0 ,0 ,0}, {1 ,1 ,1 ,1 ,0}, {0 ,0 ,0 ,0 ,1}, {1 ,1 ,1 ,1 ,0}},
    //6
    {{0 ,1 ,1 ,1 ,0}, {1 ,0 ,0 ,0 ,0}, {1 ,1 ,1 ,1 ,0}, {1 ,0 ,0 ,0 ,1}, {0 ,1 ,1 ,1 ,0}}
};

int alphabetMask[8][5][5] = {
    // :
    {{0, 0, 0, 0, 0}, {0, 0, 1, 0, 0}, {0, 0, 0, 0, 0}, {0, 0, 1, 0, 0}, {0, 0, 0, 0, 0}},
    // A
    {{0, 1, 1, 1, 0}, {1, 0, 0, 0, 1}, {1, 1, 1, 1, 1}, {1, 0, 0, 0, 1}, {1, 0, 0, 0, 1}},
    // B
    {{1, 1, 1, 1, 0}, {1, 0, 0, 0, 1}, {1, 1, 1, 1, 0}, {1, 0, 0, 0, 1}, {1, 1, 1, 1, 0}},
    // C
    {{0, 1, 1, 1, 0}, {1, 0, 0, 0, 1}, {1, 0, 0, 0, 0}, {1, 0, 0, 0 ,1}, {0 ,1 ,1 ,1 ,0}},
    // D
    {{1 ,1 ,1 ,0 ,0}, {1 ,0 ,0 ,1 ,0}, {1 ,0 ,0 ,0 ,1}, {1 ,0 ,0 ,1 ,0}, {1 ,1 ,1 ,0 ,0}},
    // E
    {{1 ,1 ,1 ,1 ,1}, {1 ,0 ,0 ,0 ,0}, {1 ,1 ,1 ,1 ,0}, {1 ,0 ,0 ,0 ,0}, {1 ,1 ,1 ,1 ,1}},
    // F
    {{1 ,1 ,1 ,1 ,1}, {1 ,0 ,0 ,0 ,0}, {1 ,1 ,1 ,1 ,0}, {1 ,0 ,0 ,0 ,0}, {1 ,0 ,0 ,0 ,0}},
    // {space}
    {{0, 0, 0, 0, 0}, {0, 0, 0, 0, 0}, {0, 0, 0, 0, 0}, {0, 0, 0, 0, 0}, {0, 0, 0, 0, 0}}
};


enum DisplayState: uint32_t {
    DISPLAY_ID,
    DISPLAY_HOST_MAC,
    DISPLAY_TARGET_MAC
};

enum DisplayEvent: uint32_t {
    EVENT_NONE = 0x00,
    EVENT_SINGLE_CLICK = 0x01,
    EVENT_DOUBLE_CLICK = 0x02,
    EVENT_LONG_HOLD = 0x04
};

// ready MAC address
uint8_t hostMacAddr[6];
uint8_t targetMacAddr[6];

int buttonState = 0;
int clickCount = 0;
uint32_t state = DISPLAY_ID;
uint32_t lastMillis = 0;
uint32_t status = 0;

String receivedString = "";
RoboCommand_t sendCommand = {
    .velocity = {.x = 0.0, .y = 0.0, .omega = 0.0},
    .WEAPON_FLAGS = {.FLAGS = 0x00},
    .RGBLED = {.RGB = {.RED = 0, .GREEN = 0, .BLUE = 0}},
    .hp = 0
};


void displayMask(int mask[5][5], int r, int g, int b)
{
    for (int y = 0; y < 5; y++)
    {
        for (int x = 0; x < 5; x++)
        {
            if (mask[y][x] == 1)
            {                
                M5.dis.drawpix(x, y, (r << 16) | (g << 8) | b);
            }
            else
            {
                M5.dis.drawpix(x, y, status);
            }
        }
    }
}

void displayID(int id)
{
    int r = colorPallet[id][0];
    int g = colorPallet[id][1];
    int b = colorPallet[id][2];

    displayMask(numberMask[id], r, g, b);
}

void displayHex(uint8_t s, int r, int g, int b)
{
    switch(s){
        case '0':
            displayMask(numberMask[0], r, g, b);
            break;
        case '1':
            displayMask(numberMask[1], r, g, b);
            break;
        case '2':
            displayMask(numberMask[2], r, g, b);
            break;
        case '3':
            displayMask(numberMask[3], r, g, b);
            break;
        case '4':
            displayMask(numberMask[4], r, g, b);
            break;
        case '5':
            displayMask(numberMask[5], r, g, b);
            break;
        case '6':
            displayMask(numberMask[6], r, g, b);
            break;
        case '7':
            displayMask(numberMask[7], r, g, b);
            break;
        case '8':
            displayMask(numberMask[8], r, g, b);
            break;
        case '9':
            displayMask(numberMask[9], r, g, b);
            break;
        case ':':
            displayMask(alphabetMask[0], r, g, b);
            break;
        case 'A':
            displayMask(alphabetMask[1], r, g, b);
            break;
        case 'B':
            displayMask(alphabetMask[2], r, g, b);
            break;
        case 'C':
            displayMask(alphabetMask[3], r, g, b);
            break;
        case 'D':
            displayMask(alphabetMask[4], r, g, b);
            break;
        case 'E':
            displayMask(alphabetMask[5], r, g, b);
            break;
        case 'F':
            displayMask(alphabetMask[6], r, g, b);
            break;
        case ' ':
            displayMask(alphabetMask[7], r, g, b);
            break;
        default:
            break;            
    }
}

void displayHostMac()
{
    int r = 255;
    int g = 0;
    int b = 0;

    String mac = WiFi.macAddress();

    for(int i=0; i<mac.length(); i++) {
        uint8_t s = mac[i];
        displayHex(s, r, g, b);
        delay(1000);
    }
    displayHex(' ', r, g, b);
    delay(1000);
}

void displayTargetMac()
{
    int r = 0;
    int g = 0;
    int b = 255;

    String mac = TARGET_MAC;

    for(int i=0; i<mac.length(); i++) {
        uint8_t s = mac[i];
        displayHex(s, r, g, b);
        delay(1000);
    }
    displayHex(' ', r, g, b);
    delay(1000);
}

void displayLoopTask(void* parameter)
{
    while(true)
    {
        switch(state){
            case DISPLAY_ID:
                displayID(DEVICE_ID);
                break;
            case DISPLAY_HOST_MAC:
                displayHostMac();
                break;
            case DISPLAY_TARGET_MAC:
                displayTargetMac();
                break;
        }

        vTaskDelay(100 / portTICK_PERIOD_MS);
    }
}

void setup()
{
    // Start serial communication for debugging    
    Serial.begin(115200);

    // Initialize the M5Atom (UART, I2C, LED)
    M5.begin(true, false, true);

    // get host mac address
    WiFi.macAddress(hostMacAddr);
    // parse target mac address
    sscanf(TARGET_MAC, "%hhx:%hhx:%hhx:%hhx:%hhx:%hhx",
        &targetMacAddr[0], &targetMacAddr[1], &targetMacAddr[2], 
        &targetMacAddr[3], &targetMacAddr[4], &targetMacAddr[5]);

    // Display controll task start
    xTaskCreate(displayLoopTask, "displayLoopTask", 4096, NULL, 1, NULL);

    // Initialize ROBO_WCOM library
    ROBO_WCOM::Init(hostMacAddr, targetMacAddr, millis(), 1000);
    
    // Reserve serial buffer
    receivedString.reserve(200);
}

void loop()
{
    // just update M5 state
    M5.update();

    // get current time
    uint32_t t = millis();

    // get button event
    if(M5.Btn.wasPressed()){
        clickCount++;
        buttonState = 1;
    }
    else if(M5.Btn.wasReleased()){
        buttonState = 0;
    }

    // event detection
    uint32_t event = EVENT_NONE;

    // long hold event
    if(t-M5.Btn.lastChange() > 2000 and buttonState == 1){
        event |= EVENT_LONG_HOLD;
    }

    // single click or double click event
    if(t-M5.Btn.lastChange() > 500 and buttonState == 0){
        if(clickCount == 1){
            event |= EVENT_SINGLE_CLICK;
        }
        else if(clickCount == 2){
            event |= EVENT_DOUBLE_CLICK;
        }
        clickCount = 0;
    }

    // state transition
    switch(state){
        case DISPLAY_ID:
            if(event & EVENT_SINGLE_CLICK){
                state = DISPLAY_HOST_MAC;
            }
            else if(event & EVENT_DOUBLE_CLICK){
                state = DISPLAY_TARGET_MAC;
            }
            break;
        case DISPLAY_HOST_MAC:
            if(event & EVENT_SINGLE_CLICK){
                state = DISPLAY_ID;
            }
            break;
        case DISPLAY_TARGET_MAC:
            if(event & EVENT_SINGLE_CLICK){
                state = DISPLAY_ID;
            }
            break;
    }

    int x, y, w;

    // receive command from pc
    if(Serial.available() > 0){
        char c = Serial.read();
        switch(c){
            // STX
            case 0x02:
                receivedString = "";
                break;
            // ETX
            case 0x03:
                if (receivedString.length() > 0) {
                    x = receivedString.substring( 0,  5).toInt();
                    y = receivedString.substring( 6, 11).toInt();
                    w = receivedString.substring(12, 17).toInt();

                    sendCommand.velocity.x = x * 0.01;
                    sendCommand.velocity.y = y * 0.01;
                    sendCommand.velocity.omega = w * 0.01;
                }
                break;
            default:
                receivedString += c;
                break;
        }
    }

    // send data for robot from host
    ROBO_WCOM::SendPacket(t,
        reinterpret_cast<uint8_t*>(&sendCommand), sizeof(RoboCommand_t));

    // recv data from robot
    if(t - lastMillis > 500) {
        uint32_t rcvTimeStamp;
        uint8_t rcvAddress[6];        
        RoboStatus_t rcvStatus;
        uint8_t rcvSize;

        auto ret = ROBO_WCOM::PopOldestPacket(t,
            &rcvTimeStamp, rcvAddress, reinterpret_cast<uint8_t*>(&rcvStatus), &rcvSize);
        switch(ret){
            case ROBO_WCOM::Status::Ok:
                status = 0xffffff;
                break;
            default:
                status = 0x000000;
                break;
        }

        lastMillis = t;
    }        

    delay(10);
}