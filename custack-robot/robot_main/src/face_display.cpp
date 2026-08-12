#include "face_display.h"



FaceDisplay::FaceDisplay() : _canvas(&M5.Display) {
    // コンストラクタ（必要に応じて初期化処理を記述）
    _width = 0;
    _height = 0;
    _center_x = 0;
    _center_y = 0;
}

void FaceDisplay::begin() {
    // M5.dis は M5.begin() で初期化されるため、ここでは明るさ設定などを行う
    M5.Display.wakeup();
    M5.Display.setBrightness(80);

    M5.Speaker.setVolume(200);
    M5.Speaker.setAllChannelVolume(200);

    _width = M5.Display.width();
    _height = M5.Display.height();

    _center_x = _width / 2;
    _center_y = _height / 2;

    // キャンバスの初期化
    _canvas.createSprite(_width, _height);
    _canvas.fillScreen(TFT_BLACK);
    _canvas.pushSprite(0, 0);

    // 起動音を鳴らす
    wakeupSound();
}


void FaceDisplay::update(
    int face_type, uint32_t now,
    bool info_enable, RobotStatus* robot_status) {
    static uint32_t last_face_ms = 0;

    if (now - last_face_ms < 50) { return; }
    last_face_ms = now;

    // 顔の描画開始
    _canvas.fillScreen(TFT_BLACK);

    switch (face_type) {
        case 0:
            idleFace(now);
            break;
        case 1:
            battleFace(now);
            break;
        default:
            break;
    }

    // テキスト情報表示
    if (info_enable) {
        // バッテリ情報表示
        batteryDisplay(
            robot_status->battery_status,
            robot_status->batt_value
        );

        // ESP-NOW通信状態表示
        espnowDisplay(
            robot_status->espnow_status
        );

        // 5V供給情報表示
        poweronDisplay(
            robot_status->poweron_enable
        );

        // 通信データの確認表示
        comdataDisplay(
            robot_status->vx,
            robot_status->vy,
            robot_status->omega,
            robot_status->rarm,
            robot_status->larm,
            robot_status->watchdog
        );

        // MACアドレス表示
        macDisplay(robot_status->mac);

        // 各ユニット状態の表示
        unitstatusDisplay(
            robot_status->leg_status,
            robot_status->rarm_status,
            robot_status->larm_status
        );
    }

    // 顔の描画終了
    _canvas.pushSprite(0, 0);
}


void FaceDisplay::clear() {
    // M5.Display.clear();
    _canvas.fillScreen(TFT_BLACK);
    _canvas.pushSprite(0, 0);
}

void FaceDisplay::idleFace(uint32_t now) {
    uint32_t span = now % 5000;

    if (span < 4500) {
        // 目を描画する
        _canvas.fillEllipse(
            _center_x - 60, _center_y - 20,
            4, 4, TFT_WHITE
        );
        _canvas.fillEllipse(
            _center_x + 60, _center_y - 20,
            4, 4, TFT_WHITE
        );

        // 口を描画する
        _canvas.fillRoundRect(
            _center_x - 50, _center_y + 20,
            100, 8, 4, TFT_WHITE
        );
    } else {
        uint32_t anim = span - 4500;
        float rate = anim / 500.0f;

        // 目を描画する
        _canvas.fillEllipse(
            _center_x - 60, _center_y - 20,
            4, (int)(4 * rate), TFT_WHITE
        );
        _canvas.fillEllipse(
            _center_x + 60, _center_y - 20,
            4, (int)(4 * rate), TFT_WHITE
        );

        // 口を描画する
        _canvas.fillRoundRect(
            _center_x - 50, _center_y + 20,
            100, 8, 4, TFT_WHITE
        );
    }
}

void FaceDisplay::battleFace(uint32_t now) {
    int rx = random(-5, 5);
    int ry = random(-5, 5);

    int cx = _center_x + rx;
    int cy = _center_y + ry;

    // 目を描画する
    _canvas.fillEllipse(
        cx - 60, cy - 20,
        6, 6, TFT_WHITE
    );
    _canvas.fillEllipse(
        cx + 60, cy - 20,
        6, 6, TFT_WHITE
    );

    // 口を描画する
    _canvas.fillTriangle(
        cx - 30, cy + 20,
        cx + 30, cy + 20,
        cx - 20, cy + 60,
        TFT_WHITE
    );
    _canvas.fillTriangle(
        cx + 30, cy + 20,
        cx - 20, cy + 60,
        cx + 20, cy + 60,
        TFT_WHITE
    );
}

void FaceDisplay::batteryDisplay(int status, float batt) {
    // バッテリ情報を表示
    _canvas.setTextSize(2);
    _canvas.setCursor(135, 10);
    switch(status) {
        case 0:
            _canvas.setTextColor(TFT_CYAN, TFT_BLACK);
            break;
        case 1:
            _canvas.setTextColor(TFT_YELLOW, TFT_BLACK);
            _canvas.printf("WARNING!!");
            break;
        case 2:
            _canvas.setTextColor(TFT_RED, TFT_BLACK);
            _canvas.printf("CRITICAL!!");
            break;
        case 3:
            _canvas.setTextColor(TFT_CYAN, TFT_BLACK);
            _canvas.printf("(USB)");
            break;
        default:
            break;
    }
    _canvas.setCursor(10, 10);
    _canvas.printf("Bat: %.2fV", batt);
}

void FaceDisplay::espnowDisplay(bool status) {
    _canvas.setTextSize(2);
    _canvas.setCursor(10, 30);
    _canvas.setTextColor(TFT_WHITE, TFT_BLACK);
    _canvas.printf("COM:");
    if (status) {
        _canvas.setTextColor(TFT_GREEN, TFT_BLACK);
        _canvas.printf("ON");
    } else {
        _canvas.setTextColor(TFT_RED, TFT_BLACK);
        _canvas.printf("OFF");
    }
}

void FaceDisplay::poweronDisplay(bool sw) {
    _canvas.setTextSize(2);
    _canvas.setCursor(135, 30);
    _canvas.setTextColor(TFT_WHITE, TFT_BLACK);
    _canvas.printf("5V:");
    if (sw) {
        _canvas.setTextColor(TFT_GREEN, TFT_BLACK);
        _canvas.printf("ON");
    } else {
        _canvas.setTextColor(TFT_RED, TFT_BLACK);
        _canvas.printf("OFF");
    }
}

void FaceDisplay::comdataDisplay(
        int16_t vx, int16_t vy, int16_t omega,
        uint8_t rarm, uint8_t larm,
        uint8_t watchdog) {
    _canvas.setTextSize(2);
    _canvas.setTextColor(TFT_ORANGE, TFT_BLACK);
    _canvas.setCursor(10, 50);
    _canvas.printf("Vx:%4d Vy:%4d W:%4d", vx, vy, omega);

    _canvas.setCursor(10, 70);
    _canvas.printf("R:%1d L:%1d T:%3d", rarm, larm, watchdog);
}

void FaceDisplay::macDisplay(String& mac) {
    _canvas.setTextSize(2);
    _canvas.setTextColor(TFT_WHITE, TFT_BLACK);

    _canvas.setCursor(10, 180);
    _canvas.printf("%s", mac.c_str());
}

void FaceDisplay::unitstatusDisplay(
        bool leg_status,
        bool rarm_status,
        bool larm_status) {

    _canvas.setTextSize(2);
    _canvas.setCursor(10, 200);

    _canvas.setTextColor(TFT_WHITE, TFT_BLACK);
    _canvas.printf("LEG:");
    if (leg_status) {
        _canvas.setTextColor(TFT_GREEN, TFT_BLACK);
        _canvas.printf("ON ");
    } else {
        _canvas.setTextColor(TFT_RED, TFT_BLACK);
        _canvas.printf("-- ");
    }

    _canvas.setTextColor(TFT_WHITE, TFT_BLACK);
    _canvas.printf("RARM:");
    if (rarm_status) {
        _canvas.setTextColor(TFT_GREEN, TFT_BLACK);
        _canvas.printf("ON ");
    } else {
        _canvas.setTextColor(TFT_RED, TFT_BLACK);
        _canvas.printf("-- ");
    }

    _canvas.setTextColor(TFT_WHITE, TFT_BLACK);
    _canvas.printf("LARM:");
    if (larm_status) {
        _canvas.setTextColor(TFT_GREEN, TFT_BLACK);
        _canvas.printf("ON ");
    } else {
        _canvas.setTextColor(TFT_RED, TFT_BLACK);
        _canvas.printf("-- ");
    }
}

void FaceDisplay::wakeupSound() {
   // 起動ビープ音 (ピピッ)
    M5.Speaker.tone(2400, 80);
    delay(100);
    M5.Speaker.tone(3200, 80);
}

void FaceDisplay::infoSound(bool sw) {
    if (sw) {
        M5.Speaker.tone(3200, 80);
    } else {
        M5.Speaker.tone(2000, 80);
    }
}

void FaceDisplay::powerSound(bool sw) {
    if (sw) {
        M5.Speaker.tone(3000, 80);
        delay(50);
        M5.Speaker.tone(3600, 80);
    } else {
        M5.Speaker.tone(3600, 80);
        delay(50);
        M5.Speaker.tone(3000, 80);
    }
}