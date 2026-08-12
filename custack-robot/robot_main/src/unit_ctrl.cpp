#include <M5Unified.h>
#include <Wire.h>
#include "unit_ctrl.h"
#include "i2c_protocol.h"


UnitControl::UnitControl() {
    // コンストラクタ（必要に応じて初期化処理を記述）
    _leg_unit_found = false;
    _rarm_unit_found = false;
    _larm_unit_found = false;
}

void UnitControl::begin(int sda, int scl) {
    //( M5.dis は M5.begin() で初期化されるため、ここでは明るさ設定などを行う

    // M5.Ex_I2C.begin(sda, scl, I2C_FREQ);
    M5.Ex_I2C.begin();
}


void UnitControl::update(uint32_t now, 
    int16_t vx, int16_t vy, int16_t omega,
    uint8_t rarm, uint8_t larm, uint8_t watchdog) {
    // 最終更新時刻を保存
    static uint32_t last_unit_ms = 0;

    // ユニットスキャンを実行
    if (now - last_unit_ms > 500) {
        last_unit_ms = now;

        // 脚ユニットを確認する
        if (!_leg_unit_found) {
            int id = checkUnit(I2C_ADDR_LEG);
            if (id > 0) {
                _leg_unit_found = true;
            }
        }
        // 右アームユニットを確認する
        if(!_rarm_unit_found) {
            int id = checkUnit(I2C_ADDR_ARM_RIGHT);
            if (id > 0) {
                _rarm_unit_found = true;
            }
        }
        // 左アームユニットを確認する
        if(!_larm_unit_found) {
            int id = checkUnit(I2C_ADDR_ARM_LEFT);
            if (id > 0) {
                _larm_unit_found = true;
            }
        }
    }

    if (_leg_unit_found) {
        I2cLegData data = {
            .vx = vx,
            .vy = vy,
            .omega = omega
        };

        bool ok1 = M5.Ex_I2C.writeRegister(
            I2C_ADDR_LEG, REG_LEG_VX_L, (uint8_t*)&data, 6, I2C_FREQ
        );
        bool ok2 = M5.Ex_I2C.writeRegister(
            I2C_ADDR_LEG, REG_WATCHDOG, (uint8_t*)&watchdog, 1, I2C_FREQ
        );

        if(!(ok1 && ok2)) {
            _leg_unit_found = false;
        }
    }

    if (_rarm_unit_found) {
        I2cArmData data;
        if (rarm > 0) {
            data.duty = 15u;
            data.pattern = ARM_PATTERN_BLINK;
        } else {
            data.duty = 0u;
            data.pattern = ARM_PATTERN_BLINK;
        }

        bool ok1 = M5.Ex_I2C.writeRegister(
            I2C_ADDR_ARM_RIGHT, REG_ARM_DUTY, (uint8_t*)&data, 2, I2C_FREQ
        );
        bool ok2 = M5.Ex_I2C.writeRegister(
            I2C_ADDR_ARM_RIGHT, REG_WATCHDOG, (uint8_t*)&watchdog, 1, I2C_FREQ
        );

        if(!(ok1 && ok2)) {
            _rarm_unit_found = false;
        }
    }

    if (_larm_unit_found) {
        I2cArmData data;
        if (larm > 0) {
            data.duty = 15u;
            data.pattern = ARM_PATTERN_BLINK;
        } else {
            data.duty = 0u;
            data.pattern = ARM_PATTERN_BLINK;
        }

        bool ok1 = M5.Ex_I2C.writeRegister(
            I2C_ADDR_ARM_LEFT, REG_ARM_DUTY, (uint8_t*)&data, 2, I2C_FREQ
        );
        bool ok2 = M5.Ex_I2C.writeRegister(
            I2C_ADDR_ARM_LEFT, REG_WATCHDOG, (uint8_t*)&watchdog, 1, I2C_FREQ
        );

        if(!(ok1 && ok2)) {
            _larm_unit_found = false;
        }
    }
}

int UnitControl::checkUnit(uint8_t addr) {
    if (scanUnit(addr)) {
        int id = getDeviceID(addr);
        if (id > 0) {
            return id;
        }
    }

    return -1;
}

bool UnitControl::scanUnit(uint8_t addr) {
    return M5.Ex_I2C.scanID(addr, I2C_FREQ);
}

int UnitControl::getDeviceID(uint8_t addr) {
    uint8_t buf;
    bool ok = M5.Ex_I2C.readRegister(addr, REG_DEVICE_ID, &buf, 1, I2C_FREQ);
    if (ok) {
        return (int)buf;
    }

    return -1;
}