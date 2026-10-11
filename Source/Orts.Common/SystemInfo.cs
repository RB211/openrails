// COPYRIGHT 2009 - 2023 by the Open Rails project.
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
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;

namespace ORTS.Common
{
    public static class SystemInfo
    {
        static readonly Regex NameAndVersionRegex = new Regex("^(.*?) +([0-9.]+)$");

        static SystemInfo()
        {
            Application = new Platform
            {
                Name = ApplicationInfo.ProductName,
                Version = VersionInfo.VersionOrBuild,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            };

            var runtime = NameAndVersionRegex.Match(RuntimeInformation.FrameworkDescription.Trim());
            Runtime = new Platform
            {
                Name = runtime.Groups[1].Value,
                Version = runtime.Groups[2].Value,
            };

            if (System.OperatingSystem.IsWindows())
                InitializeWindows();
            else
                InitializeUnix();
        }

        static void InitializeWindows()
        {
            try
            {
                // Almost nothing will correctly identify Windows 11 at this point, so we have to use WMI.
                var operatingSystem = new ManagementClass("Win32_OperatingSystem").GetInstances().Cast<ManagementObject>().First();
                OperatingSystem = new Platform
                {
                    Name = (string)operatingSystem["Caption"],
                    Version = (string)operatingSystem["Version"],
                    Architecture = RuntimeInformation.OSArchitecture.ToString(),
                    Language = CultureInfo.CurrentUICulture.IetfLanguageTag,
                    Languages = (string[])operatingSystem["MUILanguages"],
                };
            }
            catch (Exception error)
            {
                // Likely to catch multiple exceptions like:
                // Exception thrown: 'System.IO.InvalidDataException' in ORTS.Menu.dll
                Trace.WriteLine(error);
            }

            InstalledMemoryMB = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);

            try
            {
                CPUs = new ManagementClass("Win32_Processor").GetInstances().Cast<ManagementObject>().Select(processor => new CPU
                {
                    Name = (string)processor["Name"],
                    Manufacturer = (string)processor["Manufacturer"],
                    ThreadCount = (uint)processor["ThreadCount"],
                    MaxClockMHz = (uint)processor["MaxClockSpeed"],
                }).ToList();
            }
            catch (Exception error)
            {
                Trace.WriteLine(error);
            }

            // The WMI data for AdapterRAM is unreliable (it is capped at 4 GB), but it is all we have without DXGI.
            try
            {
                GPUs = new ManagementClass("Win32_VideoController").GetInstances().Cast<ManagementObject>().Select(adapter => new GPU
                {
                    Name = (string)adapter["Name"],
                    Manufacturer = (string)adapter["AdapterCompatibility"],
                    MemoryMB = (uint)(Convert.ToInt64(adapter["AdapterRAM"] ?? 0L) / 1024 / 1024),
                }).ToList();
            }
            catch (Exception error)
            {
                Trace.WriteLine(error);
            }

            var featureLevels = new uint[] {
                NativeMethods.D3D_FEATURE_LEVEL_12_2,
                NativeMethods.D3D_FEATURE_LEVEL_12_1,
                NativeMethods.D3D_FEATURE_LEVEL_12_0,
                NativeMethods.D3D_FEATURE_LEVEL_11_1,
                NativeMethods.D3D_FEATURE_LEVEL_11_0,
                NativeMethods.D3D_FEATURE_LEVEL_10_1,
                NativeMethods.D3D_FEATURE_LEVEL_10_0,
                NativeMethods.D3D_FEATURE_LEVEL_9_3,
                NativeMethods.D3D_FEATURE_LEVEL_9_2,
                NativeMethods.D3D_FEATURE_LEVEL_9_1,
            };
            foreach (var featureLevel in featureLevels)
            {
                var levels = new uint[] { featureLevel };
                try
                {
                    var rv = NativeMethods.D3D11CreateDevice(IntPtr.Zero, NativeMethods.D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, 0, levels, levels.Length, NativeMethods.D3D11_SDK_VERSION, IntPtr.Zero, out uint level, IntPtr.Zero);
                    if (level == featureLevel) Direct3DFeatureLevels.Add(string.Format("{0}_{1}", level >> 12 & 0xF, level >> 8 & 0xF));
                }
                catch (EntryPointNotFoundException) { }
                catch (DllNotFoundException) { }
            }
        }

        static void InitializeUnix()
        {
            var osName = RuntimeInformation.OSDescription;
            var osVersion = Environment.OSVersion.Version.ToString();
            try
            {
                // /etc/os-release is the standard way to identify a Linux distribution.
                if (File.Exists(ORTS.IO.PathResolver.Resolve("/etc/os-release")))
                {
                    var release = File.ReadAllLines(ORTS.IO.PathResolver.Resolve("/etc/os-release"))
                        .Select(line => line.Split(new[] { '=' }, 2))
                        .Where(parts => parts.Length == 2)
                        .ToDictionary(parts => parts[0], parts => parts[1].Trim('"'));
                    if (release.TryGetValue("NAME", out var name)) osName = name;
                    if (release.TryGetValue("VERSION_ID", out var version)) osVersion = version;
                    else if (release.TryGetValue("BUILD_ID", out var build)) osVersion = build;
                }
            }
            catch (Exception error)
            {
                Trace.WriteLine(error);
            }
            OperatingSystem = new Platform
            {
                Name = osName,
                Version = osVersion,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Language = CultureInfo.CurrentUICulture.IetfLanguageTag,
                Languages = new[] { CultureInfo.CurrentUICulture.Name },
            };

            InstalledMemoryMB = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);

            try
            {
                if (File.Exists(ORTS.IO.PathResolver.Resolve("/proc/cpuinfo")))
                {
                    var fields = File.ReadAllLines(ORTS.IO.PathResolver.Resolve("/proc/cpuinfo"))
                        .Select(line => line.Split(new[] { ':' }, 2))
                        .Where(parts => parts.Length == 2)
                        .Select(parts => (Key: parts[0].Trim(), Value: parts[1].Trim()))
                        .ToList();
                    var mhz = fields.Where(f => f.Key == "cpu MHz").Select(f => double.TryParse(f.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0).DefaultIfEmpty(0).Max();
                    CPUs.Add(new CPU
                    {
                        Name = fields.FirstOrDefault(f => f.Key == "model name").Value ?? "Unknown",
                        Manufacturer = fields.FirstOrDefault(f => f.Key == "vendor_id").Value ?? "Unknown",
                        ThreadCount = (uint)Environment.ProcessorCount,
                        MaxClockMHz = (uint)mhz,
                    });
                }
            }
            catch (Exception error)
            {
                Trace.WriteLine(error);
            }
            // GPUs are reported by the graphics backend once it has started.
        }

        public static readonly Platform Application;
        public static readonly Platform Runtime;
        public static Platform OperatingSystem { get; private set; }
        public static int InstalledMemoryMB { get; private set; }
        public static List<CPU> CPUs { get; private set; } = new List<CPU>();
        public static List<GPU> GPUs { get; private set; } = new List<GPU>();
        public static readonly List<string> Direct3DFeatureLevels = new List<string>();

        public static void WriteSystemDetails(TextWriter output)
        {
            output.WriteLine("Date/time   = {0} ({1:u})",
            DateTime.Now, DateTime.UtcNow);
            output.WriteLine("Application = {0} {1} ({2})", Application.Name, Application.Version, Application.Architecture);
            output.WriteLine("Runtime     = {0} {1}", Runtime.Name, Runtime.Version);
            output.WriteLine("System      = {0} {1} ({2}; {3}; {4})", OperatingSystem.Name, OperatingSystem.Version, OperatingSystem.Architecture, OperatingSystem.Language, string.Join(",", OperatingSystem.Languages ?? new string[0]));
            output.WriteLine("Memory      = {0:N0} MB", InstalledMemoryMB);
            foreach (var cpu in CPUs) output.WriteLine("CPU         = {0} ({1}; {2} threads; {3:N0} MHz)", cpu.Name, cpu.Manufacturer, cpu.ThreadCount, cpu.MaxClockMHz);
            foreach (var gpu in GPUs) output.WriteLine("GPU         = {0} ({1}; {2:N0} MB)", gpu.Name, gpu.Manufacturer, gpu.MemoryMB);
            output.WriteLine("Direct3D    = {0}", string.Join(",", Direct3DFeatureLevels));
        }

        public struct Platform
        {
            public string Name;
            public string Version;
            public string Architecture;
            public string Language;
            public string[] Languages;
        }

        public struct CPU
        {
            public string Name;
            public string Manufacturer;
            public uint ThreadCount;
            public uint MaxClockMHz;
        }

        public struct GPU
        {
            public string Name;
            public string Manufacturer;
            public uint MemoryMB;
        }

        static class NativeMethods
        {

            public const uint D3D11_SDK_VERSION = 7;

            public const uint D3D_DRIVER_TYPE_HARDWARE = 1;

            public const uint D3D_FEATURE_LEVEL_9_1 = 0x9100;
            public const uint D3D_FEATURE_LEVEL_9_2 = 0x9200;
            public const uint D3D_FEATURE_LEVEL_9_3 = 0x9300;
            public const uint D3D_FEATURE_LEVEL_10_0 = 0xA000;
            public const uint D3D_FEATURE_LEVEL_10_1 = 0xA100;
            public const uint D3D_FEATURE_LEVEL_11_0 = 0xB000;
            public const uint D3D_FEATURE_LEVEL_11_1 = 0xB100;
            public const uint D3D_FEATURE_LEVEL_12_0 = 0xC000;
            public const uint D3D_FEATURE_LEVEL_12_1 = 0xC100;
            public const uint D3D_FEATURE_LEVEL_12_2 = 0xC200;

            [DllImport("d3d11.dll", ExactSpelling = true)]
            public static extern uint D3D11CreateDevice(
                IntPtr adapter,
                uint driverType,
                IntPtr software,
                uint flags,
                [In] uint[] featureLevels,
                int featureLevelCount,
                uint sdkVersion,
                IntPtr device,
                out uint featureLevel,
                IntPtr immediateContext
            );
        }
    }
}
