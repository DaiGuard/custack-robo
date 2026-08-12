#pragma once

#include <string>
#include <cstring>
#include <atomic>
#include <stdexcept>
#include <fcntl.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <unistd.h>

namespace custack_router {

enum class ShmMode {
    CreateOrOpen, // 作成 (既存なら開く)
    ReadOnly,     // 読み取り専用で開く
    ReadWrite     // 既存の共有メモリを読み書きで開く
};

template <typename T>
class PosixSharedMemory {
public:
    PosixSharedMemory()
        : name_(""), fd_(-1), data_(nullptr), is_creator_(false) {}

    ~PosixSharedMemory() {
        close();
    }

    // コピー禁止、ムーブ許可
    PosixSharedMemory(const PosixSharedMemory&) = delete;
    PosixSharedMemory& operator=(const PosixSharedMemory&) = delete;

    PosixSharedMemory(PosixSharedMemory&& other) noexcept
        : name_(std::move(other.name_)),
          fd_(other.fd_),
          data_(other.data_),
          is_creator_(other.is_creator_) {
        other.fd_ = -1;
        other.data_ = nullptr;
        other.is_creator_ = false;
    }

    PosixSharedMemory& operator=(PosixSharedMemory&& other) noexcept {
        if (this != &other) {
            close();
            name_ = std::move(other.name_);
            fd_ = other.fd_;
            data_ = other.data_;
            is_creator_ = other.is_creator_;
            other.fd_ = -1;
            other.data_ = nullptr;
            other.is_creator_ = false;
        }
        return *this;
    }

    /**
     * @brief 共有メモリを初期化・マップ
     * @param name 共有メモリ名 (例: "/custack_robot_poses")
     * @param mode モード (CreateOrOpen, ReadOnly, ReadWrite)
     * @return 成功した場合 true
     */
    bool init(const std::string& name, ShmMode mode = ShmMode::CreateOrOpen) {
        close();
        name_ = name;

        int oflag = 0;
        int prot = 0;

        switch (mode) {
            case ShmMode::CreateOrOpen:
                oflag = O_CREAT | O_RDWR;
                prot = PROT_READ | PROT_WRITE;
                break;
            case ShmMode::ReadOnly:
                oflag = O_RDONLY;
                prot = PROT_READ;
                break;
            case ShmMode::ReadWrite:
                oflag = O_RDWR;
                prot = PROT_READ | PROT_WRITE;
                break;
        }

        mode_t permissions = 0666; // 全ユーザーから読み書き可能 (Unity等の別プロセス連携用)
        fd_ = ::shm_open(name_.c_str(), oflag, permissions);
        if (fd_ < 0) {
            return false;
        }

        if (mode == ShmMode::CreateOrOpen) {
            struct stat s{};
            if (::fstat(fd_, &s) == 0 && s.st_size < static_cast<off_t>(sizeof(T))) {
                if (::ftruncate(fd_, sizeof(T)) != 0) {
                    ::close(fd_);
                    fd_ = -1;
                    return false;
                }
                is_creator_ = true;
            }
        }

        void* mapped = ::mmap(nullptr, sizeof(T), prot, MAP_SHARED, fd_, 0);
        if (mapped == MAP_FAILED) {
            ::close(fd_);
            fd_ = -1;
            return false;
        }

        data_ = static_cast<T*>(mapped);
        if (is_creator_) {
            std::memset(data_, 0, sizeof(T));
        }

        return true;
    }

    void close() {
        if (data_ != nullptr && data_ != MAP_FAILED) {
            ::munmap(data_, sizeof(T));
            data_ = nullptr;
        }
        if (fd_ >= 0) {
            ::close(fd_);
            fd_ = -1;
        }
    }

    void unlink() {
        if (!name_.empty()) {
            ::shm_unlink(name_.c_str());
        }
    }

    T* get() { return data_; }
    const T* get() const { return data_; }
    bool is_valid() const { return data_ != nullptr; }

    /**
     * @brief Seqlock 方式で共有メモリへ安全にデータを書き込む
     * @tparam DataType 構造体 T と同じ型
     * @param input_data 書き込むデータ (sequence は自動インクリメントされる)
     */
    void write_seqlock(const T& input_data) {
        if (!data_) return;

        // 1. sequence を奇数にして「書き込み中」を通知
        uint32_t current_seq = data_->sequence;
        data_->sequence = current_seq + 1;
        std::atomic_thread_fence(std::memory_order_release);

        // 2. ペイロードをコピー (sequence フィールド以外または全体)
        T temp = input_data;
        temp.sequence = current_seq + 2; // 完了後の偶数シーケンス番号を設定
        
        // メモリコピー
        std::memcpy(data_, &temp, sizeof(T));

        // 3. 書き込み完了フェンス
        std::atomic_thread_fence(std::memory_order_release);
    }

    /**
     * @brief Seqlock 方式で共有メモリから安全に最新データを読み出す
     * @param out_data 読み出し先
     * @param max_retries 最大リトライ回数
     * @return 読み取り成功時 true, 競合や無効時 false
     */
    bool read_seqlock(T& out_data, int max_retries = 10) const {
        if (!data_) return false;

        for (int retry = 0; retry < max_retries; ++retry) {
            uint32_t seq1 = data_->sequence;

            // 奇数の場合は書き込み中
            if (seq1 & 1) {
                continue;
            }

            std::atomic_thread_fence(std::memory_order_acquire);

            // ローカルにコピー
            std::memcpy(&out_data, data_, sizeof(T));

            std::atomic_thread_fence(std::memory_order_acquire);

            uint32_t seq2 = data_->sequence;

            // コピー前後のシーケンスが一致し、かつ奇数（書き込み中）でなければ成功
            if (seq1 == seq2 && !(seq2 & 1)) {
                return true;
            }
        }
        return false;
    }

private:
    std::string name_;
    int fd_;
    T* data_;
    bool is_creator_;
};

} // namespace custack_router
