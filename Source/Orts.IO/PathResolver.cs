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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace ORTS.IO
{
    /// <summary>
    /// Maps paths written for Windows onto the real filesystem.
    /// </summary>
    /// <remarks>
    /// MSTS and Open Rails content was authored on Windows: it uses '\' separators and refers to files with
    /// whatever capitalisation the author typed (e.g. "..\..\GLOBAL\SHAPES\x.s" for "global/shapes/X.s").
    /// On case-sensitive filesystems <see cref="Resolve"/> converts the separators and finds each path
    /// component case-insensitively. On Windows it returns the path unchanged.
    /// Call it wherever a path that may come from content is used to read from the filesystem.
    /// </remarks>
    public static class PathResolver
    {
        static readonly bool Enabled = Path.DirectorySeparatorChar != '\\';

        /// <summary>Directory listings: real directory path -> (lower-case name -> real name).</summary>
        static readonly ConcurrentDictionary<string, DirectoryListing> Listings = new ConcurrentDictionary<string, DirectoryListing>(StringComparer.Ordinal);

        sealed class DirectoryListing
        {
            public DateTime LastWriteTimeUtc;
            public Dictionary<string, string> Names;
        }

        /// <summary>
        /// Returns the real path for <paramref name="path"/> if it (or a case-insensitive match) exists; otherwise
        /// returns the separator-normalised path, so that callers get the usual "not found" behaviour.
        /// </summary>
        public static string Resolve(string path)
        {
            if (!Enabled || string.IsNullOrEmpty(path))
                return path;

            var normalised = path.Replace('\\', '/');
            if (File.Exists(normalised) || Directory.Exists(normalised))
                return normalised;

            var full = Path.GetFullPath(normalised);
            var root = Path.GetPathRoot(full);
            var parts = full.Substring(root.Length).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var i = 0; i < parts.Length; i++)
            {
                var exact = Path.Combine(current, parts[i]);
                var isLast = i == parts.Length - 1;
                if (isLast ? (File.Exists(exact) || Directory.Exists(exact)) : Directory.Exists(exact))
                {
                    current = exact;
                    continue;
                }
                var match = FindEntry(current, parts[i]);
                if (match == null)
                    return normalised;
                current = Path.Combine(current, match);
            }
            return current;
        }

        static string FindEntry(string directory, string name)
        {
            DirectoryInfo info;
            DateTime lastWrite;
            try
            {
                info = new DirectoryInfo(directory);
                lastWrite = info.LastWriteTimeUtc;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return null;
            }

            // Re-read the listing if the directory has changed since we cached it (files may be created at run time).
            if (!Listings.TryGetValue(directory, out var listing) || listing.LastWriteTimeUtc != lastWrite)
            {
                var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var entry in info.EnumerateFileSystemInfos())
                        // If two entries differ only by case, keep the first; there is no right answer.
                        names.TryAdd(entry.Name, entry.Name);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    return null;
                }
                listing = new DirectoryListing { LastWriteTimeUtc = lastWrite, Names = names };
                Listings[directory] = listing;
            }
            return listing.Names.TryGetValue(name, out var match) ? match : null;
        }
    }
}
