#include <Arduino.h>
#include "serial_command.h"

#define BUFFER_SIZE 10

namespace {
    // シリアルコマンドの受信バッファ
    String recv_buffer[BUFFER_SIZE];
    const int recv_size = BUFFER_SIZE;
    int recv_len = 0;

    // シリアルコマンドの送信バッファ
    String send_buffer[BUFFER_SIZE];
    const int send_size = BUFFER_SIZE;
    int send_len = 0;
}

SerialCommand::SerialCommand() {
    // コンストラクタ（必要に応じて初期化処理を記述）
}

void SerialCommand::begin() {
    // シリアル通信の初期化
    Serial.begin(115200);
    // Serial.begin(460800);
}

void SerialCommand::update() {
    static String s_recv_buf = "";

    // 受信コマンドの更新処理
    while (Serial.available() > 0) {
        char c = (char)Serial.read();

        switch (c) {
            case '\r':
                break;
            case '\n':
                s_recv_buf.trim();
                s_recv_buf.toUpperCase();
                if (s_recv_buf.length() >= 3) {
                    recv_buffer[recv_len] = s_recv_buf;
                    recv_len = (recv_len + 1) % recv_size;
                }
                s_recv_buf = "";
                break;
            default:
                if (s_recv_buf.length() < 128) {
                    s_recv_buf += c;
                } else {
                    s_recv_buf = "";
                }
                break;
        }
    }

    // 送信コマンドの更新処理
    for (int i=0; i<send_len; i++) {
        if (send_buffer[i].length() > 0) {
            Serial.println(send_buffer[i]);
            send_buffer[i] = "";
        }
    }
    send_len = 0;
}

int SerialCommand::pop(String& command) {
    // 受信バッファからコマンドを取得
    if (recv_len > 0) {
        command = recv_buffer[(recv_len - 1 + recv_size) % recv_size];
        recv_len--;
        return recv_len;
    } else {
        return -1;
    }
}

bool SerialCommand::push(String message) {
    // 送信バッファにコマンドを追加
    if (send_len < send_size) {
        send_buffer[send_len] = message;
        send_len++;
        return true;
    } else {
        return false;
    }
}

bool SerialCommand::parseSET(
    const char* command, 
    int* vx, int* vy, int* omega, int* rarm, int* larm) {

    int result = sscanf(command,
        "SET,%d,%d,%d,%d,%d",
        vx, vy, omega,
        rarm, larm);

    if (result == 5) {
        return true;
    } 

    return false;
}

bool SerialCommand::parseVEL(
    const char* command, 
    int* vx, int* vy, int* omega) {

    int result = sscanf(command,
        "VEL,%d,%d,%d",
        vx, vy, omega);

    if (result == 3) {
        return true;
    } 

    return false;
}

bool SerialCommand::parseARM(
    const char* command, 
    int* rarm, int* larm) {

    int result = sscanf(command,
        "ARM,%d,%d",
        rarm, larm);

    if (result == 2) {
        return true;
    } 

    return false;
}

bool SerialCommand::parseCAL(
    const char* command,
    int* unit_id, int* cal_addr, int* cal_val) {

    int result = sscanf(command,
        "CAL,%d,%d,%d",
        unit_id, cal_addr, cal_val);

    if (result == 3) {
        return true;
    }

    return false;
}

bool SerialCommand::parseTGT(const char* command, String& mac) {
    char buf[32] = {0};
    if (parseTGT(command, buf, sizeof(buf))) {
        mac = String(buf);
        return true;
    }
    return false;
}

bool SerialCommand::parseTGT(const char* command, char* mac, size_t max_len) {
    if (command == nullptr || mac == nullptr || max_len == 0) {
        return false;
    }
    const char* p = strchr(command, ',');
    if (p == nullptr) {
        p = strchr(command, ' ');
    }
    if (p != nullptr) {
        p++;
        while (*p == ' ') p++;
        if (strlen(p) >= 17) {
            strncpy(mac, p, max_len - 1);
            mac[max_len - 1] = '\0';
            size_t len = strlen(mac);
            while (len > 0 && (mac[len - 1] == ' ' || mac[len - 1] == '\r' || mac[len - 1] == '\n')) {
                mac[--len] = '\0';
            }
            return (len >= 17);
        }
    }
    return false;
}

void SerialCommand::error(String message) {
    // デバッグメッセージの表示
#if DEBUG_LEVEL > 0
    Serial.println("[E]:" + message);
#endif
}

void SerialCommand::warn(String message) {
    // デバッグメッセージの表示
#if DEBUG_LEVEL > 1
    Serial.println("[W]:" + message);
#endif
}

void SerialCommand::info(String message) {
    // デバッグメッセージの表示
#if DEBUG_LEVEL > 2
    Serial.println("[I]:" + message);
#endif
}

void SerialCommand::debug(String message) {
    // デバッグメッセージの表示
#if DEBUG_LEVEL > 3
    Serial.println("[D]:" + message);
#endif
}