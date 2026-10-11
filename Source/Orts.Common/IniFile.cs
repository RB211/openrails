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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ORTS.Common
{
    /// <summary>
    /// Managed replacement for the Win32 GetPrivateProfileString/WritePrivateProfileString family.
    /// Like those functions, every call reads the file afresh (Menu and RunActivity may both write it),
    /// section and key names are case-insensitive, and lines it does not change (comments, ordering,
    /// unknown sections) are preserved.
    /// </summary>
    public static class IniFile
    {
        static readonly object Lock = new object();

        /// <summary>
        /// Returns the names of all sections in the file, in file order.
        /// </summary>
        public static string[] GetSectionNames(string filePath)
        {
            lock (Lock)
                return ReadLines(filePath).Select(ParseSectionHeader).Where(name => name != null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>
        /// Returns all key/value pairs in the section, in file order. Earlier keys win over later duplicates.
        /// </summary>
        public static KeyValuePair<string, string>[] GetSection(string section, string filePath)
        {
            lock (Lock)
            {
                var result = new List<KeyValuePair<string, string>>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in EnumerateSection(ReadLines(filePath), section))
                    if (seen.Add(key))
                        result.Add(new KeyValuePair<string, string>(key, value));
                return result.ToArray();
            }
        }

        /// <summary>
        /// Returns the value of the key in the section, or <c>null</c> if it does not exist.
        /// </summary>
        public static string GetValue(string section, string key, string filePath)
        {
            lock (Lock)
                foreach (var (name, value) in EnumerateSection(ReadLines(filePath), section))
                    if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                        return value;
            return null;
        }

        /// <summary>
        /// Sets the value of the key in the section, creating the section and/or key as needed.
        /// A <c>null</c> value deletes the key; a <c>null</c> key deletes the whole section.
        /// </summary>
        public static void WriteValue(string section, string key, string value, string filePath)
        {
            lock (Lock)
            {
                var lines = ReadLines(filePath).ToList();
                var sectionStart = -1;
                var sectionEnd = lines.Count;
                for (var i = 0; i < lines.Count; i++)
                {
                    var header = ParseSectionHeader(lines[i]);
                    if (header == null)
                        continue;
                    if (sectionStart >= 0)
                    {
                        sectionEnd = i;
                        break;
                    }
                    if (string.Equals(header, section, StringComparison.OrdinalIgnoreCase))
                        sectionStart = i;
                }

                if (key == null)
                {
                    if (sectionStart >= 0)
                        lines.RemoveRange(sectionStart, sectionEnd - sectionStart);
                }
                else if (sectionStart < 0)
                {
                    if (value == null)
                        return;
                    if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0)
                        lines.Add("");
                    lines.Add("[" + section + "]");
                    lines.Add(key + "=" + value);
                }
                else
                {
                    var keyLine = -1;
                    var lastKeyLine = sectionStart;
                    for (var i = sectionStart + 1; i < sectionEnd; i++)
                    {
                        var (name, _) = ParseKeyValue(lines[i]);
                        if (name == null)
                            continue;
                        lastKeyLine = i;
                        if (keyLine < 0 && string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                            keyLine = i;
                    }
                    if (keyLine >= 0)
                    {
                        if (value == null)
                            lines.RemoveAt(keyLine);
                        else
                            lines[keyLine] = key + "=" + value;
                    }
                    else if (value != null)
                    {
                        lines.Insert(lastKeyLine + 1, key + "=" + value);
                    }
                }

                WriteLines(filePath, lines);
            }
        }

        static IEnumerable<(string Key, string Value)> EnumerateSection(IEnumerable<string> lines, string section)
        {
            var inSection = false;
            foreach (var line in lines)
            {
                var header = ParseSectionHeader(line);
                if (header != null)
                {
                    inSection = string.Equals(header, section, StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inSection)
                    continue;
                var (key, value) = ParseKeyValue(line);
                if (key != null)
                    yield return (key, value);
            }
        }

        static string ParseSectionHeader(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '[')
                return null;
            var end = trimmed.IndexOf(']');
            return end > 0 ? trimmed.Substring(1, end - 1).Trim() : null;
        }

        static (string Key, string Value) ParseKeyValue(string line)
        {
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] == ';' || trimmed[0] == '#' || trimmed[0] == '[')
                return (null, null);
            var equals = trimmed.IndexOf('=');
            if (equals <= 0)
                return (null, null);
            var value = trimmed.Substring(equals + 1).Trim();
            // Windows strips one level of matching quotes around the value.
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0])
                value = value.Substring(1, value.Length - 2);
            return (trimmed.Substring(0, equals).Trim(), value);
        }

        static string[] ReadLines(string filePath)
        {
            if (!File.Exists(filePath))
                return Array.Empty<string>();
            // Windows INI files may be ANSI or UTF-16 (with BOM); File.ReadAllLines detects BOMs and defaults to UTF-8.
            return Retry(() => File.ReadAllLines(filePath));
        }

        static void WriteLines(string filePath, List<string> lines)
        {
            // Keep the existing file's encoding where it has a BOM (Windows writes UTF-16 INI files that way).
            var encoding = DetectEncoding(filePath) ?? new UTF8Encoding(false);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var newLine = OperatingSystem.IsWindows() ? "\r\n" : "\n";
            Retry(() =>
            {
                File.WriteAllText(filePath, string.Join(newLine, lines) + newLine, encoding);
                return true;
            });
        }

        static Encoding DetectEncoding(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            using (var reader = new StreamReader(filePath, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
            {
                reader.Peek();
                var encoding = reader.CurrentEncoding;
                return encoding is UTF8Encoding utf8 && utf8.GetPreamble().Length == 0 ? null : encoding;
            }
        }

        /// <summary>
        /// Another process (Menu vs RunActivity) may briefly hold the file; retry a few times.
        /// </summary>
        static T Retry<T>(Func<T> action)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return action();
                }
                catch (IOException) when (attempt < 10)
                {
                    Thread.Sleep(20);
                }
            }
        }
    }
}
