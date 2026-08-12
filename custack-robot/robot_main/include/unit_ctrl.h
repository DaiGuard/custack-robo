#ifndef UNIT_CONTROL_H
#define UNIT_CONTROL_H


class UnitControl {
private:
    bool _leg_unit_found;
    bool _rarm_unit_found;
    bool _larm_unit_found;

public:
    UnitControl();

    /**
     * @brief 
     * 
     * @param sda 
     * @param scl 
     * @param freq 
     */
    void begin(int sda, int scl);

    /**
     * @brief 
     * 
     * @param now 
     * @param vx 
     * @param vy 
     * @param omega 
     * @param rarm 
     * @param larm 
     */
    void update(uint32_t now,
        int16_t vx, int16_t vy, int16_t omega,
        uint8_t rarm, uint8_t larm,
        uint8_t watchdog);

    /**
     * @brief 
     * 
     * @param addr 
     * @return int 
     */
    int  checkUnit(uint8_t addr);

    /**
     * @brief 
     * 
     * @param addr 
     * @return true 
     * @return false 
     */
    bool scanUnit(uint8_t addr);

    /**
     * @brief Get the Device I D object
     * 
     * @param addr 
     * @return int 
     */
    int  getDeviceID(uint8_t addr);

    bool legUnitFound(){ return _leg_unit_found; }
    bool rarmUnitFound(){ return _rarm_unit_found; }
    bool larmUnitFound(){ return _larm_unit_found; }
};


#endif // UNIT_CONTROL_H