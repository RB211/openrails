// COPYRIGHT 2022 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Orts.Processes;
using ORTS.Common;

namespace Orts.Viewer3D.Processes
{
    /// <summary>
    /// The host process is used to collect and provide details about the
    /// host environment and the application's consumption of resources,
    /// such as CPU, GPU, and memory usage.
    /// </summary>
    public class HostProcess
    {
        public int ProcessorCount { get; } = System.Environment.ProcessorCount;
        public float CLRMemoryAllocatedBytesPerSec { get; private set; }
        public float CPUMemoryPrivate { get; private set; }
        public float CPUMemoryWorkingSet { get; private set; }
        public float CPUMemoryWorkingSetPrivate { get; private set; }
        public float CPUMemoryVirtual { get; private set; }
        public ulong CPUMemoryVirtualLimit { get; private set; }
        public float GPUMemoryCommitted { get; private set; }
        public float GPUMemoryDedicated { get; private set; }
        public float GPUMemoryShared { get; private set; }

        long LastAllocatedBytes;
        DateTime LastAllocatedTime;

        readonly Profiler Profiler = new Profiler("Host");
        readonly ProcessState State = new ProcessState("Host");
        readonly Game Game;
        readonly Thread Thread;

        private const int SleepTime = 10000;

        public HostProcess(Game game)
        {
            Debug.Assert(GC.MaxGeneration == 2, "Runtime is expected to have a MaxGeneration of 2.");

            Game = game;
            Thread = new Thread(HostThread);
            // Set before any thread starts: the viewer derives its loading memory threshold from it.
            CPUMemoryVirtualLimit = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        }

        public void Start()
        {
            Thread.Start();
        }

        public void Stop()
        {
            State.SignalTerminate();
        }

        [ThreadName("Host")]
        void HostThread()
        {
            Profiler.SetThread();

            LastAllocatedBytes = GC.GetTotalAllocatedBytes();
            LastAllocatedTime = DateTime.UtcNow;

            while (true)
            {
                State.Sleep(SleepTime);
                if (State.Terminated)
                    break;
                if (!DoHost())
                    return;
            }
        }

        [CallOnThread("Host")]
        bool DoHost()
        {
            if (Debugger.IsAttached)
            {
                Host();
            }
            else
            {
                try
                {
                    Host();
                }
                catch (Exception error)
                {
                    // We ignore all errors because the data is non-critical
                    // Trace.WriteLine(error);
                    Game.ProcessReportError(error);
                }
            }
            return true;
        }

        [CallOnThread("Host")]
        void Host()
        {
            Profiler.Start();
            try
            {
                var now = DateTime.UtcNow;
                var allocatedBytes = GC.GetTotalAllocatedBytes();
                var seconds = (now - LastAllocatedTime).TotalSeconds;
                CLRMemoryAllocatedBytesPerSec = seconds > 0 ? (float)((allocatedBytes - LastAllocatedBytes) / seconds) : 0;
                LastAllocatedBytes = allocatedBytes;
                LastAllocatedTime = now;

                using (var process = Process.GetCurrentProcess())
                {
                    CPUMemoryPrivate = process.PrivateMemorySize64;
                    CPUMemoryWorkingSet = process.WorkingSet64;
                    CPUMemoryVirtual = process.VirtualMemorySize64;
                }
                CPUMemoryWorkingSetPrivate = OperatingSystem.IsLinux() ? ReadLinuxPrivateWorkingSet() : CPUMemoryPrivate;

                if (OperatingSystem.IsLinux())
                    ReadLinuxGpuMemory();
            }
            finally
            {
                Profiler.Stop();
            }
        }

        /// <summary>
        /// Resident anonymous memory, the closest Linux equivalent of Windows' "Working Set - Private".
        /// </summary>
        static float ReadLinuxPrivateWorkingSet()
        {
            foreach (var line in File.ReadLines(ORTS.IO.PathResolver.Resolve("/proc/self/status")))
                if (line.StartsWith("RssAnon:"))
                    return ParseKiB(line.Substring(8));
            return 0;
        }

        /// <summary>
        /// Reads per-process GPU memory from the DRM client usage stats that Linux kernel drivers (amdgpu,
        /// i915, xe, nouveau, ...) publish in /proc/self/fdinfo. Each open DRM file is a client; sum them.
        /// </summary>
        void ReadLinuxGpuMemory()
        {
            float dedicated = 0, shared = 0;
            var seenClients = new HashSet<string>();
            foreach (var fdinfo in Directory.EnumerateFiles(ORTS.IO.PathResolver.Resolve("/proc/self/fdinfo")))
            {
                string[] lines;
                try { lines = File.ReadAllLines(ORTS.IO.PathResolver.Resolve(fdinfo)); }
                catch (IOException) { continue; }
                var clientId = lines.FirstOrDefault(l => l.StartsWith("drm-client-id:"));
                if (clientId == null || !seenClients.Add(clientId))
                    continue;
                foreach (var line in lines)
                {
                    // drm-memory-<region> is the older key, drm-resident-<region> the newer one.
                    if (line.StartsWith("drm-memory-vram:") || line.StartsWith("drm-resident-vram:") || line.StartsWith("drm-resident-local:"))
                        dedicated += ParseKiB(line.Substring(line.IndexOf(':') + 1));
                    else if (line.StartsWith("drm-memory-gtt:") || line.StartsWith("drm-resident-gtt:") || line.StartsWith("drm-resident-system:"))
                        shared += ParseKiB(line.Substring(line.IndexOf(':') + 1));
                }
            }
            GPUMemoryDedicated = dedicated;
            GPUMemoryShared = shared;
            GPUMemoryCommitted = dedicated + shared;
        }

        static float ParseKiB(string value)
        {
            var parts = value.Trim().Split(' ');
            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
                return 0;
            var unit = parts.Length > 1 ? parts[1] : "";
            return unit == "KiB" || unit == "kB" ? number * 1024 : unit == "MiB" ? number * 1024 * 1024 : unit == "GiB" ? number * 1024 * 1024 * 1024 : number;
        }
    }
}
