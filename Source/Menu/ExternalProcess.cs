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
using System.Diagnostics;
using System.IO;
using System.Linq;
using ORTS.Common;

namespace Menu
{
    /// <summary>
    /// Cross-platform helpers for starting the simulator, tools, documents and web pages.
    /// </summary>
    public static class ExternalProcess
    {
        /// <summary>
        /// The file name of one of our executables (apphost): "RunActivity.exe" on Windows, "RunActivity" elsewhere.
        /// </summary>
        public static string ExecutableName(string baseName)
        {
            return OperatingSystem.IsWindows() ? baseName + ".exe" : baseName;
        }

        /// <summary>
        /// Full path of one of our executables, which live alongside the Menu.
        /// </summary>
        public static string ExecutablePath(string baseName)
        {
            return Path.Combine(ApplicationInfo.ProcessDirectory, ExecutableName(baseName));
        }

        /// <summary>
        /// Opens a document, folder or URL with the user's default application (xdg-open on Linux).
        /// </summary>
        public static void Open(string fileNameOrUrl)
        {
            Process.Start(new ProcessStartInfo { FileName = fileNameOrUrl, UseShellExecute = true });
        }

        /// <summary>
        /// Starts one of our programs with the given arguments, each passed as a separate argument.
        /// </summary>
        public static Process Start(string executablePath, IEnumerable<string> arguments)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                WorkingDirectory = ApplicationInfo.ProcessDirectory,
            };
            foreach (var argument in arguments)
                processStartInfo.ArgumentList.Add(argument);
            return Process.Start(processStartInfo);
        }

        /// <summary>
        /// Formats the arguments as a command line for display (or pasting into a shell / debugger).
        /// </summary>
        public static string FormatCommandLine(IEnumerable<string> arguments)
        {
            return String.Join(" ", arguments.Select(QuoteArgument));
        }

        static string QuoteArgument(string argument)
        {
            if (argument.Length > 0 && !argument.Any(c => Char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '$' || c == '\\' && !OperatingSystem.IsWindows()))
                return argument;
            if (OperatingSystem.IsWindows())
                return "\"" + argument.Replace("\"", "\\\"") + "\"";
            return "'" + argument.Replace("'", "'\\''") + "'";
        }

        /// <summary>
        /// Starts a console program so that the user can see its output and keep using the console afterwards
        /// (the equivalent of <c>cmd /k program</c> on Windows).
        /// </summary>
        /// <returns>False if no terminal emulator could be found.</returns>
        public static bool StartInTerminal(string executablePath, IEnumerable<string> arguments)
        {
            var commandLine = FormatCommandLine(new[] { executablePath }.Concat(arguments));
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("cmd", "/k \"" + commandLine + "\"") { WorkingDirectory = ApplicationInfo.ProcessDirectory });
                return true;
            }

            // Run the program and then leave an interactive shell open, like "cmd /k".
            var shell = Environment.GetEnvironmentVariable("SHELL");
            if (String.IsNullOrEmpty(shell)) shell = "/bin/sh";
            var script = commandLine + "; exec " + QuoteArgument(shell);

            if (OperatingSystem.IsMacOS())
            {
                var osa = new ProcessStartInfo("osascript") { UseShellExecute = false };
                osa.ArgumentList.Add("-e");
                osa.ArgumentList.Add("tell application \"Terminal\" to do script \"" + script.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");
                Process.Start(osa);
                return true;
            }

            // Prefer the user's own choice, then common terminals. Most accept "-e program args...".
            var terminals = new List<string>();
            var userTerminal = Environment.GetEnvironmentVariable("TERMINAL");
            if (!String.IsNullOrEmpty(userTerminal)) terminals.Add(userTerminal);
            terminals.AddRange(new[] { "x-terminal-emulator", "xdg-terminal-exec", "kgx", "gnome-terminal", "konsole", "xfce4-terminal", "alacritty", "kitty", "foot", "wezterm", "ghostty", "xterm" });

            foreach (var terminal in terminals)
            {
                var terminalPath = FindOnPath(terminal);
                if (terminalPath == null)
                    continue;

                var startInfo = new ProcessStartInfo(terminalPath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = ApplicationInfo.ProcessDirectory,
                };
                switch (Path.GetFileName(terminal))
                {
                    case "xdg-terminal-exec":
                    case "kitty":
                    case "foot":
                    case "ghostty":
                        break;
                    case "gnome-terminal":
                    case "kgx":
                        startInfo.ArgumentList.Add("--");
                        break;
                    case "wezterm":
                        startInfo.ArgumentList.Add("start");
                        startInfo.ArgumentList.Add("--");
                        break;
                    default:
                        startInfo.ArgumentList.Add("-e");
                        break;
                }
                startInfo.ArgumentList.Add(shell);
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add(script);
                try
                {
                    Process.Start(startInfo);
                    return true;
                }
                catch
                {
                    // Try the next terminal.
                }
            }
            return false;
        }

        static string FindOnPath(string program)
        {
            if (Path.IsPathRooted(program))
                return File.Exists(program) ? program : null;
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(directory, program);
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }
    }
}
