#ifndef UNIT_CONTROL_H
#define UNIT_CONTROL_H

#include <stdint.h>

class UnitControl {
private:
    bool _leg_unit_found;
    bool _rarm_unit_found;
    bool _larm_unit_found;

    uint8_t _leg_id;
    uint8_t _rarm_id;
    uint8_t _larm_id;

    uint32_t _last_scan_ms;

public:
    UnitControl();

    /**
     * @brief I2C初期化
     */
    void begin(int sda, int scl);

    /**
     * @brief 定周期更新 (制御指令送信 & 定期IDスキャン)
     */
    void update(uint32_t now,
        int16_t vx, int16_t vy, int16_t omega,
        uint8_t rarm, uint8_t larm,
        uint8_t watchdog);

    int  checkUnit(uint8_t addr);
    bool scanUnit(uint8_t addr);
    int  getDeviceID(uint8_t addr);

    bool legUnitFound() const { return _leg_unit_found; }
    bool rarmUnitFound() const { return _rarm_unit_found; }
    bool larmUnitFound() const { return _larm_unit_found; }

    uint8_t getLegId() const { return _leg_id; }
    uint8_t getRightArmId() const { return _rarm_id; }
    uint8_t getLeftArmId() const { return _larm_id; }
};

#endif // UNIT_CONTROL_H