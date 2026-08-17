using System;
using System.Runtime.InteropServices;

namespace Custack.SharedMemory
{
    /// <summary>
    /// 単一ロボットの位置姿勢およびデバイス情報構造体 (20 bytes, Pack=1)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SharedRobotPose
    {
        public int id;             // AprilTag ID (-1: 未検出/無効)
        public float x;            // 投影面正規化座標 (-1.0 ~ 1.0)
        public float y;            // 投影面正規化座標 (-1.0 ~ 1.0)
        public float theta;        // 回転角 (radian)
        public byte legId;         // 脚ユニット デバイスID (0x01: Omni, 0x02: Tire, 0x03: Crawler)
        public byte armRightId;    // 右アーム デバイスID (0x01: Pistol, 0x02: Gatling, 0x03: Missile)
        public byte armLeftId;     // 左アーム デバイスID (0x01: Pistol, 0x02: Gatling, 0x03: Missile)
        public byte status;        // ステータスフラグ (0: 未受信/無効, 1: 正常受信)
    }

    /// <summary>
    /// 全ロボットの位置データ共有メモリ構造体 (ROS 2 -> Unity)
    /// Seqlock (奇数: 書込中, 偶数: 確定)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SharedRobotPoseData
    {
        public const int MaxRobots = 32;
        public const uint CurrentVersion = 1;

        public uint version;
        public uint sequence;
        public ulong timestampNs;
        public uint count;
        public fixed byte posesRaw[MaxRobots * 20]; // 32台 * 20bytes = 640bytes

        public SharedRobotPose GetPose(int index)
        {
            if (index < 0 || index >= MaxRobots) return default;
            fixed (byte* p = posesRaw)
            {
                SharedRobotPose* ptr = (SharedRobotPose*)p;
                return ptr[index];
            }
        }
    }

    /// <summary>
    /// 単一コントローラの操作コマンド構造体 (10 bytes, Pack=1)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SingleControllerCommand
    {
        public short vx;          // 前後移動速度 (-1000 ~ 1000)
        public short vy;          // 左右移動速度 (-1000 ~ 1000)
        public short omega;       // 旋回速度     (-1000 ~ 1000)
        public byte armRight;     // 右アーム起動 (0: 停止, 1: 起動)
        public byte armLeft;      // 左アーム起動 (0: 停止, 1: 起動)
        public byte active;       // コントローラ有効フラグ (0: 無効, 1: 有効)
        public byte reserved;     // パディング
    }

    /// <summary>
    /// 全コントローラの操作コマンド共有メモリ構造体 (Unity -> C++)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SharedControllerData
    {
        public const int MaxControllers = 8;
        public const uint CurrentVersion = 1;

        public uint version;
        public uint sequence;
        public ulong timestampMs;
        public uint count;
        public fixed byte controllersRaw[MaxControllers * 10]; // 8台 * 10bytes = 80bytes

        public void SetController(int index, SingleControllerCommand cmd)
        {
            if (index < 0 || index >= MaxControllers) return;
            fixed (byte* p = controllersRaw)
            {
                SingleControllerCommand* ptr = (SingleControllerCommand*)p;
                ptr[index] = cmd;
            }
        }

        public SingleControllerCommand GetController(int index)
        {
            if (index < 0 || index >= MaxControllers) return default;
            fixed (byte* p = controllersRaw)
            {
                SingleControllerCommand* ptr = (SingleControllerCommand*)p;
                return ptr[index];
            }
        }
    }
}
