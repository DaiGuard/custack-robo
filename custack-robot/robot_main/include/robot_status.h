#ifndef ROBOT_STATUS_H
#define ROBOT_STATUS_H

// ロボット状態表示用変数
struct RobotStatus {
    int battery_status;
    float batt_value;
    bool espnow_status;
    bool poweron_enable;

    bool leg_status;
    bool rarm_status;
    bool larm_status;

    int16_t vx;
    int16_t vy;
    int16_t omega;
    uint8_t rarm;
    uint8_t larm;
    uint8_t watchdog;

    String mac;
};

#endif // ROBOT_STATUS_H