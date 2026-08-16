using System;
using System.Runtime.InteropServices;

namespace Custack.SharedMemory
{
    #region Shared Memory Data Structures (Layout matches C++ struct exactly)

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SharedRobotPose
    {
        public int id;          // AprilTag ID (-1: 未検出/無効)
        public float x;         // 投影面座標 -1.0 ~ 1.0
        public float y;         // 投影面座標 -1.0 ~ 1.0
        public float theta;     // 回転角 (radian)
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SharedRobotPoseData
    {
        public const int MaxRobots = 32;
        public const uint CurrentVersion = 1;

        public uint version;
        public uint sequence;
        public ulong timestampNs;
        public uint count;
        public fixed byte posesRaw[MaxRobots * 16]; // 32 * sizeof(SharedRobotPose)

        public SharedRobotPose GetPose(int index)
        {
            if (index < 0 || index >= MaxRobots) return default;
            fixed (byte* ptr = posesRaw)
            {
                SharedRobotPose* posesPtr = (SharedRobotPose*)ptr;
                return posesPtr[index];
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SingleControllerCommand
    {
        public short vx;           // 移動速度 Vx (-1000 ~ 1000)
        public short vy;           // 移動速度 Vy (-1000 ~ 1000)
        public short omega;        // 旋回速度 Omega (-1000 ~ 1000)
        public byte armRight;      // 右アーム起動フラグ (0:停止, 1:起動)
        public byte armLeft;       // 左アーム起動フラグ (0:停止, 1:起動)
        public byte active;        // コントローラ有効フラグ (0:無効, 1:有効)
        public byte reserved;      // パディング
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SharedControllerData
    {
        public const int MaxControllers = 8;
        public const uint CurrentVersion = 1;

        public uint version;
        public uint sequence;
        public ulong timestampMs;
        public uint count;
        public fixed byte controllersRaw[MaxControllers * 8]; // 8 * sizeof(SingleControllerCommand)

        public void SetController(int index, SingleControllerCommand cmd)
        {
            if (index < 0 || index >= MaxControllers) return;
            fixed (byte* ptr = controllersRaw)
            {
                SingleControllerCommand* cmdPtr = (SingleControllerCommand*)ptr;
                cmdPtr[index] = cmd;
            }
        }

        public SingleControllerCommand GetController(int index)
        {
            if (index < 0 || index >= MaxControllers) return default;
            fixed (byte* ptr = controllersRaw)
            {
                SingleControllerCommand* cmdPtr = (SingleControllerCommand*)ptr;
                return cmdPtr[index];
            }
        }
    }

    #endregion
}
