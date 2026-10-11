// COPYRIGHT 2014, 2015 by the Open Rails project.
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

using Orts.Formats.Msts;
using System.IO;

namespace Orts.Formats.OR
{
    public class MSTSData
    {
        public TrackDatabaseFile TDB { get; protected set; }
        public RouteFile TRK { get; protected set; }
        public TrackSectionsFile TSectionDat { get; protected set; }
        public SignalConfigurationFile SIGCFG { get; protected set; }
        public string RoutePath { get; set; }
        public string MstsPath { get; set; }
        public AESignals Signals { get; protected set; }

        public MSTSData (string mstsPath, string Route)
        {
            MstsPath = mstsPath;
            RoutePath = Route;
            TRK = new RouteFile(MSTS.MSTSPath.GetTRKFileName(RoutePath));
            string routePath = Path.Combine(Route, TRK.Tr_RouteFile.FileName);
            TDB = new TrackDatabaseFile(CombineIgnoreCase(RoutePath, TRK.Tr_RouteFile.FileName + ".tdb"));

            string ORfilepath = CombineIgnoreCase(RoutePath, "OpenRails");

            if (File.Exists(CombineIgnoreCase(ORfilepath, "sigcfg.dat")))
            {
                SIGCFG = new SignalConfigurationFile(CombineIgnoreCase(ORfilepath, "sigcfg.dat"), true);
            }
            else
            {
                SIGCFG = new SignalConfigurationFile(CombineIgnoreCase(RoutePath, "sigcfg.dat"), false);
            }

            if (File.Exists(CombineIgnoreCase(RoutePath, "Openrails", "TSECTION.DAT")))
                TSectionDat = new TrackSectionsFile(CombineIgnoreCase(RoutePath, "Openrails", "TSECTION.DAT"));
            else if (File.Exists(CombineIgnoreCase(RoutePath, "GLOBAL", "TSECTION.DAT")))
                TSectionDat = new TrackSectionsFile(CombineIgnoreCase(RoutePath, "GLOBAL", "TSECTION.DAT"));
            else
                TSectionDat = new TrackSectionsFile(CombineIgnoreCase(MstsPath, "GLOBAL", "TSECTION.DAT"));
            if (File.Exists(CombineIgnoreCase(RoutePath, "TSECTION.DAT")))
                TSectionDat.AddRouteTSectionDatFile(CombineIgnoreCase(RoutePath, "TSECTION.DAT"));
            Signals = new AESignals (this, SIGCFG);
        }

        /// <summary>
        /// Path.Combine, matching each part case-insensitively against existing files and folders
        /// (MSTS content has arbitrary casing, which matters on case-sensitive file systems).
        /// </summary>
        internal static string CombineIgnoreCase(string basePath, params string[] parts)
        {
            var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, IgnoreInaccessible = true };
            var path = basePath;
            foreach (var part in parts)
            {
                var candidate = Path.Combine(path, part);
                if (!System.OperatingSystem.IsWindows() && !Path.Exists(candidate) && Directory.Exists(path))
                {
                    foreach (var match in Directory.EnumerateFileSystemEntries(path, part, options))
                    {
                        candidate = match;
                        break;
                    }
                }
                path = candidate;
            }
            return path;
        }
    }
}