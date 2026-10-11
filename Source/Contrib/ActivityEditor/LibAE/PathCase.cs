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

using System;
using System.IO;
using System.Linq;

namespace LibAE
{
    /// <summary>
    /// MSTS content was authored on Windows and uses arbitrary casing for file and folder names
    /// (e.g. "TRAINS\CONSISTS" vs "Trains\Consists", "*.TRK" vs "*.trk") and backslashes as separators.
    /// These helpers resolve such paths on case-sensitive file systems.
    /// </summary>
    public static class PathCase
    {
        static readonly EnumerationOptions InsensitiveOptions = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
        };

        /// <summary>
        /// Combine <paramref name="basePath"/> with relative <paramref name="parts"/> (which may themselves contain
        /// '\' or '/' separators, and ".."), matching each component case-insensitively against what exists on disk.
        /// Components that do not exist are appended as given.
        /// </summary>
        public static string Combine(string basePath, params string[] parts)
        {
            var path = basePath;
            foreach (var part in parts.SelectMany(p => p.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)))
            {
                if (part == "..")
                {
                    path = Path.GetDirectoryName(Path.GetFullPath(path)) ?? path;
                    continue;
                }
                var candidate = Path.Combine(path, part);
                if (!OperatingSystem.IsWindows() && !Path.Exists(candidate) && Directory.Exists(path) && part.IndexOfAny(new[] { '*', '?' }) < 0)
                {
                    var match = Directory.EnumerateFileSystemEntries(path, part, InsensitiveOptions).FirstOrDefault();
                    if (match != null)
                        candidate = match;
                }
                path = candidate;
            }
            return path;
        }

        /// <summary>Case-insensitive equivalent of <see cref="Directory.GetFileSystemEntries(string, string)"/>; empty if the folder is missing.</summary>
        public static string[] GetFileSystemEntries(string path, string searchPattern)
        {
            if (!Directory.Exists(path))
                return new string[0];
            return Directory.GetFileSystemEntries(path, searchPattern, InsensitiveOptions);
        }
    }
}
