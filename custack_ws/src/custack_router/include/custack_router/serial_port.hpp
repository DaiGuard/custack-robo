#pragma once

#include <string>
#include <vector>
#include <cstring>
#include <fcntl.h>
#include <termios.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <chrono>

namespace custack_router {

class SerialPort {
public:
    SerialPort() : fd_(-1), port_name_(""), baudrate_(115200), is_connected_(false) {}

    explicit SerialPort(const std::string& port_name, int baudrate = 115200)
        : fd_(-1), port_name_(port_name), baudrate_(baudrate), is_connected_(false) {
        open(port_name_, baudrate_);
    }

    ~SerialPort() {
        close();
    }

    SerialPort(const SerialPort&) = delete;
    SerialPort& operator=(const SerialPort&) = delete;

    SerialPort(SerialPort&& other) noexcept
        : fd_(other.fd_),
          port_name_(std::move(other.port_name_)),
          baudrate_(other.baudrate_),
          is_connected_(other.is_connected_),
          rx_buffer_(std::move(other.rx_buffer_)) {
        other.fd_ = -1;
        other.is_connected_ = false;
    }

    SerialPort& operator=(SerialPort&& other) noexcept {
        if (this != &other) {
            close();
            fd_ = other.fd_;
            port_name_ = std::move(other.port_name_);
            baudrate_ = other.baudrate_;
            is_connected_ = other.is_connected_;
            rx_buffer_ = std::move(other.rx_buffer_);
            other.fd_ = -1;
            other.is_connected_ = false;
        }
        return *this;
    }

    bool open(const std::string& port_name, int baudrate = 115200) {
        close();
        port_name_ = port_name;
        baudrate_ = baudrate;

        // O_RDWR: 読み書き, O_NOCTTY: 制御端末にしない, O_NONBLOCK: ノンブロッキング
        fd_ = ::open(port_name_.c_str(), O_RDWR | O_NOCTTY | O_NONBLOCK);
        if (fd_ < 0) {
            is_connected_ = false;
            return false;
        }

        speed_t speed = B115200;
        switch (baudrate) {
            case 9600: speed = B9600; break;
            case 19200: speed = B19200; break;
            case 38400: speed = B38400; break;
            case 57600: speed = B57600; break;
            case 115200: speed = B115200; break;
            case 230400: speed = B230400; break;
            case 460800: speed = B460800; break;
            case 921600: speed = B921600; break;
            default: speed = B115200; break;
        }

        struct termios tty{};
        if (tcgetattr(fd_, &tty) != 0) {
            close();
            return false;
        }

        cfsetospeed(&tty, speed);
        cfsetispeed(&tty, speed);

        // 8N1 (8bit, No Parity, 1 Stop bit)
        tty.c_cflag &= ~PARENB;
        tty.c_cflag &= ~CSTOPB;
        tty.c_cflag &= ~CSIZE;
        tty.c_cflag |= CS8;
        tty.c_cflag &= ~CRTSCTS; // ハードウェアフロー制御なし
        tty.c_cflag |= (CLOCAL | CREAD); // 受信有効、ローカルライン

        // Raw 入力モード
        tty.c_lflag &= ~(ICANON | ECHO | ECHOE | ISIG);
        tty.c_iflag &= ~(IXON | IXOFF | IXANY); // ソフトウェアフロー制御無効
        tty.c_iflag &= ~(IGNBRK | BRKINT | PARMRK | ISTRIP | INLCR | IGNCR | ICRNL);

        // Raw 出力モード
        tty.c_oflag &= ~OPOST;
        tty.c_oflag &= ~ONLCR;

        // ノンブロッキング (即時復帰)
        tty.c_cc[VTIME] = 0;
        tty.c_cc[VMIN] = 0;

        if (tcsetattr(fd_, TCSANOW, &tty) != 0) {
            close();
            return false;
        }

        // 入出力バッファをフラッシュ
        tcflush(fd_, TCIOFLUSH);

        is_connected_ = true;
        rx_buffer_.clear();
        return true;
    }

    void close() {
        if (fd_ >= 0) {
            ::close(fd_);
            fd_ = -1;
        }
        is_connected_ = false;
    }

    bool is_open() const {
        return is_connected_ && (fd_ >= 0);
    }

    const std::string& get_port_name() const {
        return port_name_;
    }

    bool try_reconnect() {
        if (is_open()) return true;
        return open(port_name_, baudrate_);
    }

    bool write_bytes(const void* data, size_t size) {
        if (!is_open()) return false;
        ssize_t written = ::write(fd_, data, size);
        if (written < 0) {
            // エラー発生時は切断とみなす
            close();
            return false;
        }
        return static_cast<size_t>(written) == size;
    }

    bool write_string(const std::string& str) {
        return write_bytes(str.data(), str.size());
    }

    bool write_command(const std::string& cmd) {
        std::string payload = cmd;
        if (payload.empty() || payload.back() != '\n') {
            payload += '\n';
        }
        return write_string(payload);
    }

    /**
     * @brief ノンブロッキングで改行区切りの受信文字列を1行取得
     * @param out_line 取得した文字列 (改行除く)
     * @return 1行受信できた場合 true
     */
    bool read_line(std::string& out_line) {
        if (!is_open()) return false;

        char buf[256];
        ssize_t n = ::read(fd_, buf, sizeof(buf));
        if (n > 0) {
            rx_buffer_.append(buf, n);
        } else if (n < 0 && errno != EAGAIN && errno != EWOULDBLOCK) {
            close();
            return false;
        }

        size_t newline_pos = rx_buffer_.find('\n');
        if (newline_pos != std::string::npos) {
            out_line = rx_buffer_.substr(0, newline_pos);
            // キャリッジリターン除外
            if (!out_line.empty() && out_line.back() == '\r') {
                out_line.pop_back();
            }
            rx_buffer_.erase(0, newline_pos + 1);
            return true;
        }

        // バッファ肥大化防止 (最大4KB)
        if (rx_buffer_.size() > 4096) {
            rx_buffer_.clear();
        }

        return false;
    }

private:
    int fd_;
    std::string port_name_;
    int baudrate_;
    bool is_connected_;
    std::string rx_buffer_;
};

} // namespace custack_router
