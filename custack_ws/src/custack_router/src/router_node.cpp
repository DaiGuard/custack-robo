#include <chrono>
#include <memory>
#include <string>
#include <vector>
#include <sstream>
#include <iomanip>
#include <mutex>

#include "rclcpp/rclcpp.hpp"
#include "custack_msgs/msg/robot_pose_array.hpp"
#include "custack_router/shared_memory_types.h"
#include "custack_router/posix_shm.hpp"
#include "custack_router/serial_port.hpp"

using namespace std::chrono_literals;

namespace custack_router {

struct RobotTelemetry {
    uint8_t leg_id{0};
    uint8_t arm_right_id{0};
    uint8_t arm_left_id{0};
    uint16_t bat_mv{0};
    uint8_t status{0};
    bool valid{false};
    std::chrono::steady_clock::time_point last_recv_time{};
};

inline const char* get_leg_name(uint8_t id) {
    switch (id) {
        case 0x01: return "Omni";
        case 0x02: return "Tire";
        case 0x03: return "Crawler";
        default:   return "Unknown";
    }
}

inline const char* get_arm_name(uint8_t id) {
    switch (id) {
        case 0x01: return "Gatling";
        case 0x02: return "Sword";
        case 0x03: return "LaserCannon";
        default:   return "Unknown";
    }
}

class RouterNode : public rclcpp::Node {
public:
    explicit RouterNode(const rclcpp::NodeOptions& options = rclcpp::NodeOptions())
        : Node("custack_router", options),
          pose_msg_count_(0),
          last_stats_time_(std::chrono::steady_clock::now()),
          last_cmd_seq_(0),
          last_cmd_recv_time_(std::chrono::steady_clock::now()) {

        // --- パラメータ宣言 ---
        this->declare_parameter<std::string>("robot_pose_topic", "robot_poses");
        this->declare_parameter<std::string>("shm_robot_poses_name", DEFAULT_SHM_ROBOT_POSES_NAME);
        this->declare_parameter<std::string>("shm_controller_cmd_name", DEFAULT_SHM_CONTROLLER_CMD_NAME);
        this->declare_parameter<std::vector<std::string>>("serial_ports", {
            "/dev/custack_bridge_1",
            "/dev/custack_bridge_2",
            "/dev/custack_bridge_3"
        });
        this->declare_parameter<int>("baudrate", 115200);
        this->declare_parameter<double>("send_rate_hz", 50.0);
        this->declare_parameter<int>("cmd_timeout_ms", 1000);
        this->declare_parameter<bool>("enable_serial", true);
        this->declare_parameter<bool>("debug_log", false);

        // --- パラメータ取得 ---
        robot_pose_topic_ = this->get_parameter("robot_pose_topic").as_string();
        shm_robot_poses_name_ = this->get_parameter("shm_robot_poses_name").as_string();
        shm_controller_cmd_name_ = this->get_parameter("shm_controller_cmd_name").as_string();
        port_names_ = this->get_parameter("serial_ports").as_string_array();
        baudrate_ = this->get_parameter("baudrate").as_int();
        send_rate_hz_ = this->get_parameter("send_rate_hz").as_double();
        cmd_timeout_ms_ = this->get_parameter("cmd_timeout_ms").as_int();
        enable_serial_ = this->get_parameter("enable_serial").as_bool();
        debug_log_ = this->get_parameter("debug_log").as_bool();

        telemetry_.resize(std::max(port_names_.size(), MAX_SHARED_ROBOTS));

        RCLCPP_INFO(this->get_logger(), "=========================================");
        RCLCPP_INFO(this->get_logger(), "  custack_router Initializing...");
        RCLCPP_INFO(this->get_logger(), "  - Robot Pose Topic      : %s", robot_pose_topic_.c_str());
        RCLCPP_INFO(this->get_logger(), "  - Pose SHM Name         : %s", shm_robot_poses_name_.c_str());
        RCLCPP_INFO(this->get_logger(), "  - Controller SHM Name   : %s", shm_controller_cmd_name_.c_str());
        RCLCPP_INFO(this->get_logger(), "  - Baudrate              : %d bps", baudrate_);
        RCLCPP_INFO(this->get_logger(), "  - Send Rate             : %.1f Hz", send_rate_hz_);
        RCLCPP_INFO(this->get_logger(), "  - Serial Enabled        : %s", enable_serial_ ? "true" : "false");
        for (size_t i = 0; i < port_names_.size(); ++i) {
            RCLCPP_INFO(this->get_logger(), "  - Bridge [%zu] (Tag ID %zu): %s", i + 1, i + 1, port_names_[i].c_str());
        }
        RCLCPP_INFO(this->get_logger(), "=========================================");

        // --- 共有メモリ初期化 ---
        if (!shm_robot_poses_.init(shm_robot_poses_name_, ShmMode::CreateOrOpen)) {
            RCLCPP_ERROR(this->get_logger(), "Failed to initialize Shared Memory: %s", shm_robot_poses_name_.c_str());
        } else {
            RCLCPP_INFO(this->get_logger(), "✅ Shared Memory for Robot Poses ready: %s", shm_robot_poses_name_.c_str());
        }

        if (!shm_controller_cmd_.init(shm_controller_cmd_name_, ShmMode::CreateOrOpen)) {
            RCLCPP_ERROR(this->get_logger(), "Failed to initialize Shared Memory: %s", shm_controller_cmd_name_.c_str());
        } else {
            RCLCPP_INFO(this->get_logger(), "✅ Shared Memory for Controller Commands ready: %s", shm_controller_cmd_name_.c_str());
        }

        // --- シリアルポート初期化 ---
        if (enable_serial_) {
            init_serial_ports();
        }

        // --- ROS 2 サブスクライバ設定 ---
        rclcpp::QoS qos(rclcpp::KeepLast(1));
        qos.best_effort(); // 低遅延ストリーミング

        robot_pose_sub_ = this->create_subscription<custack_msgs::msg::RobotPoseArray>(
            robot_pose_topic_,
            qos,
            std::bind(&RouterNode::robot_pose_callback, this, std::placeholders::_1)
        );

        // --- タイマー設定 (コントローラ共有メモリ読み出し & シリアル送信) ---
        auto timer_period = std::chrono::duration<double>(1.0 / send_rate_hz_);
        send_timer_ = this->create_wall_timer(
            std::chrono::duration_cast<std::chrono::nanoseconds>(timer_period),
            std::bind(&RouterNode::send_timer_callback, this)
        );

        // --- テレメトリ要求タイマー (1秒周期で各シリアルポートへTLMコマンド送信) ---
        tlm_timer_ = this->create_wall_timer(
            std::chrono::seconds(1),
            std::bind(&RouterNode::tlm_timer_callback, this)
        );
    }

    ~RouterNode() override {
        RCLCPP_INFO(this->get_logger(), "Shutting down custack_router. Sending STOP commands to all serial ports...");
        send_all_stop();
        serial_ports_.clear();
    }

private:
    void init_serial_ports() {
        serial_ports_.clear();
        for (size_t i = 0; i < port_names_.size(); ++i) {
            auto port = std::make_unique<SerialPort>(port_names_[i], baudrate_);
            if (port->is_open()) {
                RCLCPP_INFO(this->get_logger(), "✅ Opened serial port [%zu]: %s", i, port_names_[i].c_str());
            } else {
                RCLCPP_WARN(this->get_logger(), "⚠️ Failed to open serial port [%zu]: %s (Will retry in background)", i, port_names_[i].c_str());
            }
            serial_ports_.push_back(std::move(port));
        }
    }

    bool parse_telemetry(const std::string& line, RobotTelemetry& out_tlm) {
        if (line.rfind("TLM,", 0) != 0 && line.rfind("TLM", 0) != 0) {
            return false;
        }

        std::stringstream ss(line);
        std::string token;
        std::vector<std::string> tokens;
        while (std::getline(ss, token, ',')) {
            size_t start = token.find_first_not_of(" \t\r\n");
            size_t end = token.find_last_not_of(" \t\r\n");
            if (start != std::string::npos && end != std::string::npos) {
                tokens.push_back(token.substr(start, end - start + 1));
            } else {
                tokens.push_back("");
            }
        }

        if (tokens.size() < 5) {
            return false;
        }

        try {
            int leg = std::stoi(tokens[1], nullptr, 0);
            int arm_r = std::stoi(tokens[2], nullptr, 0);
            int arm_l = std::stoi(tokens[3], nullptr, 0);
            int bat = std::stoi(tokens[4], nullptr, 0);

            out_tlm.leg_id = static_cast<uint8_t>(leg);
            out_tlm.arm_right_id = static_cast<uint8_t>(arm_r);
            out_tlm.arm_left_id = static_cast<uint8_t>(arm_l);
            out_tlm.bat_mv = static_cast<uint16_t>(bat);
            out_tlm.status = 1;
            out_tlm.valid = true;
            out_tlm.last_recv_time = std::chrono::steady_clock::now();
            return true;
        } catch (const std::exception&) {
            return false;
        }
    }

    void robot_pose_callback(const custack_msgs::msg::RobotPoseArray::SharedPtr msg) {
        if (!shm_robot_poses_.is_valid()) return;

        SharedRobotPoseData data{};
        data.version = SHM_PROTOCOL_VERSION;
        data.timestamp_ns = this->now().nanoseconds();
        
        size_t count = std::min(msg->poses.size(), MAX_SHARED_ROBOTS);
        data.count = static_cast<uint32_t>(count);

        std::lock_guard<std::mutex> lock(telemetry_mutex_);

        for (size_t i = 0; i < count; ++i) {
            int32_t tag_id = msg->poses[i].id;
            data.poses[i].id = tag_id;
            data.poses[i].x = msg->poses[i].x;
            data.poses[i].y = msg->poses[i].y;
            data.poses[i].theta = msg->poses[i].theta;

            // 該当 ID に対応するテレメトリ情報（タグID 1=ポート0, タグID 2=ポート1, タグID 3=ポート2）をセット
            size_t tlm_idx = (tag_id >= 1 && tag_id <= 3) ? static_cast<size_t>(tag_id - 1) : static_cast<size_t>(tag_id);
            if (tlm_idx < telemetry_.size() && telemetry_[tlm_idx].valid) {
                const auto& tlm = telemetry_[tlm_idx];
                data.poses[i].leg_id = tlm.leg_id;
                data.poses[i].arm_right_id = tlm.arm_right_id;
                data.poses[i].arm_left_id = tlm.arm_left_id;
                data.poses[i].status = tlm.status;
            } else {
                data.poses[i].leg_id = 0;
                data.poses[i].arm_right_id = 0;
                data.poses[i].arm_left_id = 0;
                data.poses[i].status = 0;
            }
        }

        // Seqlock で共有メモリへ書き込み
        shm_robot_poses_.write_seqlock(data);

        // 統計情報のカウント
        pose_msg_count_++;
        auto now = std::chrono::steady_clock::now();
        auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(now - last_stats_time_).count();
        if (elapsed >= 5) {
            double hz = static_cast<double>(pose_msg_count_) / elapsed;
            RCLCPP_INFO(this->get_logger(), "[Pose SHM Stats] Ingestion Rate: %.1f Hz (Latest Pose Count: %u)", hz, data.count);
            pose_msg_count_ = 0;
            last_stats_time_ = now;
        }
    }

    void send_timer_callback() {
        if (!enable_serial_) return;

        SharedControllerData ctrl_data{};
        bool has_data = shm_controller_cmd_.read_seqlock(ctrl_data);
        auto now = std::chrono::steady_clock::now();

        bool is_timeout = false;
        if (has_data && ctrl_data.sequence != 0) {
            if (ctrl_data.sequence != last_cmd_seq_) {
                last_cmd_seq_ = ctrl_data.sequence;
                last_cmd_recv_time_ = now;
            }
            auto time_since_last_cmd = std::chrono::duration_cast<std::chrono::milliseconds>(now - last_cmd_recv_time_).count();
            if (time_since_last_cmd > cmd_timeout_ms_) {
                is_timeout = true;
            }
        } else {
            is_timeout = true;
        }

        // 各シリアルポートに対して受信処理 & コマンド送信
        for (size_t i = 0; i < serial_ports_.size(); ++i) {
            auto& port = serial_ports_[i];

            // 未接続の場合は再接続を試行
            if (!port->is_open()) {
                if (!port->try_reconnect()) {
                    continue; // 次の周期で再試行
                }
                RCLCPP_INFO(this->get_logger(), "✅ Reconnected serial port [%zu]: %s", i, port->get_port_name().c_str());
            }

            // テレメトリ受信パース & レスポンス処理
            std::string rx_line;
            while (port->read_line(rx_line)) {
                RobotTelemetry tlm{};
                if (parse_telemetry(rx_line, tlm)) {
                    bool id_changed = false;
                    {
                        std::lock_guard<std::mutex> lock(telemetry_mutex_);
                        if (i < telemetry_.size()) {
                            if (telemetry_[i].leg_id != tlm.leg_id ||
                                telemetry_[i].arm_right_id != tlm.arm_right_id ||
                                telemetry_[i].arm_left_id != tlm.arm_left_id) {
                                id_changed = true;
                            }
                            telemetry_[i] = tlm;
                        }
                    }

                    // デバイスIDのデバッグ出力 (RCLCPP_DEBUG)
                    RCLCPP_DEBUG(this->get_logger(),
                        "[DEBUG Port %zu DeviceID] Leg: 0x%02X (%s), ArmR: 0x%02X (%s), ArmL: 0x%02X (%s), Battery: %u mV",
                        i, tlm.leg_id, get_leg_name(tlm.leg_id),
                        tlm.arm_right_id, get_arm_name(tlm.arm_right_id),
                        tlm.arm_left_id, get_arm_name(tlm.arm_left_id),
                        tlm.bat_mv);

                    if (debug_log_ || id_changed) {
                        RCLCPP_INFO(this->get_logger(),
                            "🔋 [Port %zu DeviceID] Leg: 0x%02X (%s), ArmR: 0x%02X (%s), ArmL: 0x%02X (%s), Battery: %u mV",
                            i, tlm.leg_id, get_leg_name(tlm.leg_id),
                            tlm.arm_right_id, get_arm_name(tlm.arm_right_id),
                            tlm.arm_left_id, get_arm_name(tlm.arm_left_id),
                            tlm.bat_mv);
                    }
                } else if (debug_log_) {
                    RCLCPP_DEBUG(this->get_logger(), "[Serial %zu RX] %s", i, rx_line.c_str());
                }
            }

            // コマンド生成 & 送信
            if (is_timeout || i >= ctrl_data.count || ctrl_data.controllers[i].active == 0) {
                // タイムアウトまたはコントローラ無効時は安全停止 (STP)
                port->write_command("STP");
            } else {
                const auto& cmd = ctrl_data.controllers[i];
                // SET,vx,vy,omega,arm_r,arm_l
                std::ostringstream oss;
                oss << "SET,"
                    << cmd.vx << ","
                    << cmd.vy << ","
                    << cmd.omega << ","
                    << static_cast<int>(cmd.arm_right) << ","
                    << static_cast<int>(cmd.arm_left);

                std::string cmd_str = oss.str();
                port->write_command(cmd_str);

                if (debug_log_) {
                    RCLCPP_DEBUG(this->get_logger(), "[Serial %zu TX] %s", i, cmd_str.c_str());
                }
            }
        }
    }

    void tlm_timer_callback() {
        if (!enable_serial_) return;
        for (size_t i = 0; i < serial_ports_.size(); ++i) {
            if (serial_ports_[i]->is_open()) {
                serial_ports_[i]->write_command("TLM");
            }
        }
    }

    void send_all_stop() {
        for (size_t i = 0; i < serial_ports_.size(); ++i) {
            if (serial_ports_[i]->is_open()) {
                serial_ports_[i]->write_command("STP");
            }
        }
    }

    // パラメータ
    std::string robot_pose_topic_;
    std::string shm_robot_poses_name_;
    std::string shm_controller_cmd_name_;
    std::vector<std::string> port_names_;
    int baudrate_;
    double send_rate_hz_;
    int cmd_timeout_ms_;
    bool enable_serial_;
    bool debug_log_;

    // ROS 2 & SHM & Serial
    rclcpp::Subscription<custack_msgs::msg::RobotPoseArray>::SharedPtr robot_pose_sub_;
    rclcpp::TimerBase::SharedPtr send_timer_;
    rclcpp::TimerBase::SharedPtr tlm_timer_;

    PosixSharedMemory<SharedRobotPoseData> shm_robot_poses_;
    PosixSharedMemory<SharedControllerData> shm_controller_cmd_;
    std::vector<std::unique_ptr<SerialPort>> serial_ports_;

    // テレメトリ管理
    std::mutex telemetry_mutex_;
    std::vector<RobotTelemetry> telemetry_;

    // 統計 & タイムアウト管理
    uint64_t pose_msg_count_;
    std::chrono::steady_clock::time_point last_stats_time_;
    uint32_t last_cmd_seq_;
    std::chrono::steady_clock::time_point last_cmd_recv_time_;
};

} // namespace custack_router

int main(int argc, char** argv) {
    rclcpp::init(argc, argv);
    auto node = std::make_shared<custack_router::RouterNode>();
    rclcpp::spin(node);
    rclcpp::shutdown();
    return 0;
}
