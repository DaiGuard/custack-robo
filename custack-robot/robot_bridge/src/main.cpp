#include <Arduino.h>
#include <M5Atom.h>
#include "espnow_protocol.h"
#include "matrix_display.h"
#include "serial_command.h"
#include "espnow_com.h"


// LED表示クラスのインスタンスを作成
MatrixDisplay matrixDisplay;

// シリアルコマンドクラスのインスタンスを作成
SerialCommand serialCommand;

// ESP-NOW通信クラスのインスタンスを作成
ESPNowCom espnowCom;

/**
 * @brief セットアップ
 * 
 */
void setup() {
    // M5Atom 初期化
    M5.begin(true, false, true);
    delay(100);

    // LED表示の初期化
    matrixDisplay.begin();
    matrixDisplay.test();
    matrixDisplay.clear();

    // シリアルコマンドの初期化
    serialCommand.begin();

    // ESP-NOWの初期化
    if (!espnowCom.begin()) {
        while (1) {
            serialCommand.debug("[E]: cant init espnow");
            delay(1000);
        }
    }
}

/**
 * @brief ループ処理
 * 
 */
void loop() {
    // シリアルコマンドの更新時間
    static uint32_t last_serial_ms = 0;
    // ローカル変数
    char message[32];
    int vx, vy, omega;
    int rarm, larm;
    int watchdog;

    // M5Atomの更新処理
    M5.update();

    // 現在時間の取得
    uint32_t now = millis();

    // 各種通信の状態更新
    bool serial_status = now - last_serial_ms > 1000 ? false : true;         
    bool espnow_status = espnowCom.isConnected();
    espnowCom.getPacket(&vx, &vy, &omega, &rarm, &larm, &watchdog);
    bool move_status = (vx != 0 || vy != 0 || omega != 0);
    bool rarm_status = (rarm != 0);
    bool larm_status = (larm != 0);

    // シリアルコマンドを更新
    serialCommand.update();

    // シリアルコマンドの取得
    String command = "";
    while (serialCommand.pop(command) >= 0) {
        last_serial_ms = now;
        // コマンドの解析
        if (command.startsWith("MAC")) {
            // 自身のMACアドレスを返信
            String mac = espnowCom.getOwnMac();
            serialCommand.push("MAC," + mac);
        } else if (command.startsWith("SET")) {
            // ESP-NOW通信データのセット
            if (serialCommand.parseSET(command.c_str(), &vx, &vy, &omega, &rarm, &larm)) {
                espnowCom.setVelocity(vx, vy, omega);
                espnowCom.setArm(rarm, larm);
            }
        } else if (command.startsWith("VEL")) {
            // ESP-NOW通信速度デーーたのセット
            if (serialCommand.parseVEL(command.c_str(), &vx, &vy, &omega)) {
                espnowCom.setVelocity(vx, vy, omega);
            }
        } else if (command.startsWith("ARM")) {
            // ESP-NOW通信アームユニットのセット
            if (serialCommand.parseARM(command.c_str(), &rarm, &larm)) {
                espnowCom.setArm(rarm, larm);
            }
        } else if (command.startsWith("STP")) {
            // ESP-NOW通信ストップ
            espnowCom.setStop();
        } else if (command.startsWith("TGT")) {
            sscanf(command.c_str(), "TGT,%s", message);
            if (espnowCom.registerPeer(String(message))) {
                String mac = espnowCom.getTagMac();
                serialCommand.push("TGT," + mac);
            } else {
                serialCommand.error("cant register peer");
            }
        } else if (command.startsWith("PIN")) {
            serialCommand.push("PON");
        } else if (command.startsWith("STS")) {
            espnowCom.getPacket(&vx, &vy, &omega, &rarm, &larm, &watchdog);
            sprintf(message, 
                "STS,%d,%d,%d,%d,%d,%d,%d,%d",
                serial_status ? 1 : 0,
                espnow_status ? 1 : 0,
                vx, vy, omega, rarm, larm, watchdog);
            serialCommand.push(String(message));
        }        
    }

    // シリアル通信遮断で強制停止
    if (!serial_status) {
        espnowCom.setStop();
    }

    // ESP-NOW通信の更新
    bool espnow_result = espnowCom.update(now);

    // LED表示を更新
    matrixDisplay.update(
        serial_status, 
        espnow_status,
        move_status, rarm_status, larm_status, now);
}