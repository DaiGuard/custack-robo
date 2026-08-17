#pragma once

#include <cstdint>
#include <cstddef>

namespace custack_router {

constexpr const char* DEFAULT_SHM_ROBOT_POSES_NAME = "/custack_robot_poses";
constexpr const char* DEFAULT_SHM_CONTROLLER_CMD_NAME = "/custack_controller_cmd";

constexpr size_t MAX_SHARED_ROBOTS = 32;
constexpr size_t MAX_SHARED_CONTROLLERS = 8;
constexpr uint32_t SHM_PROTOCOL_VERSION = 1;

#pragma pack(push, 1)

/**
 * @brief 個別ロボットの推定位置・姿勢およびデバイスID/ステータス
 */
struct SharedRobotPose {
    int32_t id;           // AprilTag ID (-1: 未検出)
    float x;              // 投影面座標 (-1.0 ~ 1.0)
    float y;              // 投影面座標 (-1.0 ~ 1.0)
    float theta;          // 回転角 (radian)
    uint8_t leg_id;       // 脚ユニット デバイスID (0x01: Omni, 0x02: Tire, 0x03: Crawler)
    uint8_t arm_right_id; // 右アーム デバイスID (0x01: Pistol, 0x02: Gatling, 0x03: Missile)
    uint8_t arm_left_id;  // 左アーム デバイスID (0x01: Pistol, 0x02: Gatling, 0x03: Missile)
    uint8_t status;       // ステータスフラグ (0: 未受信/無効, 1: 正常受信)
};

/**
 * @brief ロボット全機の位置データ共有メモリ構造体 (ROS 2 -> Unity)
 *        Seqlock (シーケンスロック) によるロックフリー読み書きをサポート
 */
struct SharedRobotPoseData {
    uint32_t version;                         // プロトコルバージョン (SHM_PROTOCOL_VERSION)
    uint32_t sequence;                        // Seqlock シーケンス (奇数: 書込中, 偶数: 確定)
    uint64_t timestamp_ns;                    // ROS受信タイムスタンプ (ナノ秒)
    uint32_t count;                           // 現在の検出機体数 (0 〜 MAX_SHARED_ROBOTS)
    SharedRobotPose poses[MAX_SHARED_ROBOTS]; // 各ロボットの位置姿勢・デバイス情報
};

/**
 * @brief 単一コントローラからの操作コマンド
 */
struct SingleControllerCommand {
    int16_t vx;         // 移動速度 Vx (-1000 ~ 1000)
    int16_t vy;         // 移動速度 Vy (-1000 ~ 1000)
    int16_t omega;      // 旋回速度 Omega (-1000 ~ 1000)
    uint8_t arm_right;  // 右アーム起動フラグ (0: 停止, 1: 起動)
    uint8_t arm_left;   // 左アーム起動フラグ (0: 停止, 1: 起動)
    uint8_t active;     // コントローラ接続/有効フラグ (0: 無効, 1: 有効)
    uint8_t reserved;   // アライメント用予約領域
};

/**
 * @brief 全コントローラの操作コマンド共有メモリ構造体 (Unity -> custack_router -> M5Atom)
 */
struct SharedControllerData {
    uint32_t version;                                            // プロトコルバージョン (SHM_PROTOCOL_VERSION)
    uint32_t sequence;                                           // Seqlock シーケンス (奇数: 書込中, 偶数: 確定)
    uint64_t timestamp_ms;                                       // Unity側送信タイムスタンプ (ミリ秒)
    uint32_t count;                                              // 有効コントローラ数 (通常 3)
    SingleControllerCommand controllers[MAX_SHARED_CONTROLLERS]; // 各コントローラの入力
};

#pragma pack(pop)

} // namespace custack_router
