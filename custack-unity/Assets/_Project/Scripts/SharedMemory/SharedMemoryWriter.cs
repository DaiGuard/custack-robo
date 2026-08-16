using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Custack.SharedMemory
{
    /// <summary>
    /// POSIX共有メモリ (/dev/shm/custack_controller_cmd) へ
    /// ロボット制御コマンドを Seqlock 方式で書き込むクラス。
    /// </summary>
    public class SharedMemoryWriter : IDisposable
    {
        private readonly string shmPath;
        private FileStream fileStream;
        private readonly byte[] buffer;
        private uint sequence = 0;

        public bool IsConnected => fileStream != null;
        public string ShmPath => shmPath;

        public SharedMemoryWriter(string path = "/dev/shm/custack_controller_cmd")
        {
            shmPath = path;
            buffer = new byte[Marshal.SizeOf<SharedControllerData>()];
            TryConnect();
        }

        public bool TryConnect()
        {
            if (fileStream != null) return true;

            try
            {
                fileStream = new FileStream(shmPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                if (fileStream.Length < Marshal.SizeOf<SharedControllerData>())
                {
                    fileStream.SetLength(Marshal.SizeOf<SharedControllerData>());
                }
                Debug.Log($"[SharedMemoryWriter] Initialized Command SHM: {shmPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SharedMemoryWriter] Error opening {shmPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 全コントローラのコマンドを一括 Seqlock 書き込み
        /// </summary>
        public unsafe bool WriteCommands(SingleControllerCommand[] commands, int validCount)
        {
            if (fileStream == null && !TryConnect()) return false;

            try
            {
                SharedControllerData data = default;
                data.version = SharedControllerData.CurrentVersion;
                data.timestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                data.count = (uint)Mathf.Clamp(validCount, 0, SharedControllerData.MaxControllers);

                for (int i = 0; i < commands.Length && i < SharedControllerData.MaxControllers; i++)
                {
                    data.SetController(i, commands[i]);
                }

                // Seqlock シーケンス更新 (偶数維持)
                sequence += 2;
                data.sequence = sequence;

                fixed (byte* ptr = buffer)
                {
                    Marshal.StructureToPtr(data, (IntPtr)ptr, false);
                }

                fileStream.Seek(0, SeekOrigin.Begin);
                fileStream.Write(buffer, 0, buffer.Length);
                fileStream.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SharedMemoryWriter] Write error: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            fileStream?.Dispose();
            fileStream = null;
        }
    }
}
