#include "matrix_display.h"

MatrixDisplay::MatrixDisplay() {
    // コンストラクタ（必要に応じて初期化処理を記述）
}

void MatrixDisplay::begin() {
    // M5.dis は M5.begin() で初期化されるため、ここでは明るさ設定などを行う
    M5.dis.setBrightness(80);
}

void MatrixDisplay::test() {
    // 起動時全点灯イルミネーションテスト (赤 -> 緑 -> 青 -> 白)
    M5.dis.fillpix(0x800000); delay(150);
    M5.dis.fillpix(0x008000); delay(150);
    M5.dis.fillpix(0x000080); delay(150);
    M5.dis.fillpix(0x808080); delay(200);    
}


void MatrixDisplay::update(
    bool host_status, bool robot_status,
    bool leg_active, bool rarm_active, bool larm_active,
    uint32_t now) {


    static uint32_t last_led_ms = 0;
    if (now - last_led_ms < 50) { return; }
    last_led_ms = now;

    // 点滅制御用の値を取得
    uint32_t interval = now % 200;
    bool blink = (interval > 100) ? false : true;

    // ホスト通信が有効の時は点滅
    if (host_status) {
        if(blink) {
            M5.dis.drawpix(2, 4, 0xffffff);
            M5.dis.drawpix(1, 3, 0xffffff);
            M5.dis.drawpix(2, 3, 0xffffff);
            M5.dis.drawpix(3, 3, 0xffffff);
        } else {
            M5.dis.drawpix(2, 4, 0x000000);
            M5.dis.drawpix(1, 3, 0x000000);
            M5.dis.drawpix(2, 3, 0x000000);
            M5.dis.drawpix(3, 3, 0x000000);
        }
    } else {
        M5.dis.drawpix(2, 4, 0xff0000);
        M5.dis.drawpix(1, 3, 0xff0000);
        M5.dis.drawpix(2, 3, 0xff0000);
        M5.dis.drawpix(3, 3, 0xff0000);
    }

    if (robot_status) {
        if(blink) {
            M5.dis.drawpix(2, 0, 0xffff00);
            M5.dis.drawpix(1, 1, 0xffff00);
            M5.dis.drawpix(2, 1, 0xffff00);
            M5.dis.drawpix(3, 1, 0xffff00);
        } else {
            M5.dis.drawpix(2, 0, 0x000000);
            M5.dis.drawpix(1, 1, 0x000000);
            M5.dis.drawpix(2, 1, 0x000000);
            M5.dis.drawpix(3, 1, 0x000000);
        }
    } else {
        M5.dis.drawpix(2, 0, 0xff0000);
        M5.dis.drawpix(1, 1, 0xff0000);
        M5.dis.drawpix(2, 1, 0xff0000);
        M5.dis.drawpix(3, 1, 0xff0000);        
    }

    if (leg_active) {
        M5.dis.drawpix(2, 2, 0x00ff00);
    } else {
        M5.dis.drawpix(2, 2, 0x000000);
    }

    if (rarm_active) {
        M5.dis.drawpix(0, 2, 0x0000ff);
    } else {
        M5.dis.drawpix(0, 2, 0x000000);
    }

    if (larm_active) {
        M5.dis.drawpix(4, 2, 0x0000ff);
    } else {
        M5.dis.drawpix(4, 2, 0x000000);
    }
}


void MatrixDisplay::clear() {
    M5.dis.clear();
}