// COPYRIGHT 2026 by the Open Rails project.
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
//
// TEMPORARY, LOCAL WORKAROUND. MSTS content uses arbitrary file name casing, and was written for a case-insensitive
// file system. A shared case-insensitive path resolver for all of Open Rails is being designed separately; when that
// exists, this class should be replaced by it. Only the files that TrackViewer opens itself (terrain tiles and
// terrain textures) go through this; files opened by the shared libraries (Orts.Formats.Msts etc.) do not.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace ORTS.TrackViewer.Drawing
{
    /// <summary>
    /// Minimal case-insensitive resolution of file and directory paths on case-sensitive file systems.
    /// </summary>
    public static class CaseInsensitivePath
    {
        private static readonly ConcurrentDictionary<string, string[]> directoryEntries = new ConcurrentDictionary<string, string[]>();
        private static readonly bool fileSystemIsCaseSensitive = !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS();

        /// <summary>
        /// Return a path to an existing file or directory that matches the given path case-insensitively,
        /// or the given path itself if there is none (or if the file system is case-insensitive anyway).
        /// Windows directory separators are accepted.
        /// </summary>
        public static string Resolve(string path)
        {
            if (String.IsNullOrEmpty(path)) return path;
            if (!fileSystemIsCaseSensitive) return path;
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            if (File.Exists(path) || Directory.Exists(path)) return path;

            string full;
            try { full = Path.GetFullPath(path); }
            catch { return path; }

            string root = Path.GetPathRoot(full);
            string[] parts = full.Substring(root.Length).Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            string current = root;
            foreach (string part in parts)
            {
                string candidate = Path.Combine(current, part);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                {
                    current = candidate;
                    continue;
                }
                string match = Entries(current).FirstOrDefault(e => String.Equals(e, part, StringComparison.OrdinalIgnoreCase));
                if (match == null) return path;
                current = Path.Combine(current, match);
            }
            return current;
        }

        /// <summary>Enumeration options for case-insensitive wildcard matching in Directory.GetFiles</summary>
        public static readonly EnumerationOptions IgnoreCase = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };

        /// <summary>File.Exists, but case-insensitive</summary>
        public static bool FileExists(string path) => File.Exists(Resolve(path));

        private static string[] Entries(string directory)
        {
            return directoryEntries.GetOrAdd(directory, d =>
            {
                try { return Directory.GetFileSystemEntries(d).Select(Path.GetFileName).ToArray(); }
                catch { return new string[0]; }
            });
        }
    }
}
