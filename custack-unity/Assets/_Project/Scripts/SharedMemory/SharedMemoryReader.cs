using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Custack.SharedMemory
{
    /// <summary>
    /// POSIX共有メモリ (/dev/shm/custack_robot_poses) から
    /// ロボット全機の位置姿勢を Seqlock ロックフリーで読み取るクラス。
    /// </summary>
    public class SharedMemoryReader : IDisposable
    {
        private readonly string shmPath;
        private FileStream fileStream;
        private readonly byte[] buffer;

        public bool IsConnected => fileStream != null;
        public string ShmPath => shmPath;

        public SharedMemoryReader(string path = "/dev/shm/custack_robot_poses")
        {
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("/dev/shm/") && !File.Exists(path))
            {
                shmPath = path.StartsWith("/") ? "/dev/shm" + path : "/dev/shm/" + path;
            }
            else
            {
                shmPath = path;
            }

            buffer = new byte[Marshal.SizeOf<SharedRobotPoseData>()];
            TryConnect();
        }

        public bool TryConnect()
        {
            if (fileStream != null) return true;

            try
            {
                if (File.Exists(shmPath))
                {
                    fileStream = new FileStream(shmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    Debug.Log($"[SharedMemoryReader] Connected to Pose SHM: {shmPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SharedMemoryReader] Cannot connect to {shmPath}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Seqlock ロックフリー読み取り
        /// </summary>
        public unsafe bool ReadPoses(out SharedRobotPoseData poseData)
        {
            poseData = default;
            if (fileStream == null && !TryConnect()) return false;

            for (int retry = 0; retry < 5; ++retry)
            {
                try
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                    int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead < buffer.Length) break;

                    fixed (byte* ptr = buffer)
                    {
                        SharedRobotPoseData* shmData = (SharedRobotPoseData*)ptr;
                        uint seq = shmData->sequence;

                        // 奇数は書き込み中、0は未初期化
                        if ((seq & 1) != 0 || seq == 0) continue;

                        poseData = *shmData;

                        // 前後の sequence が一致すれば整合性あり
                        if (poseData.sequence == seq)
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    break;
                }
            }

            return false;
        }

        public void Dispose()
        {
            fileStream?.Dispose();
            fileStream = null;
        }
    }
}
