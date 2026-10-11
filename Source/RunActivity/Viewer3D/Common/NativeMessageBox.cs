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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ORTS.Common;

namespace Orts.Viewer3D.Common
{
    public enum MessageBoxButtons { OK, OKCancel }
    public enum MessageBoxIcon { None, Error, Warning, Information }
    public enum DialogResult { None, OK, Cancel }

    /// <summary>
    /// Synchronous, toolkit-free message box. It must work when the game is crashing, possibly before or after
    /// the game window exists, so it uses the operating system directly: user32 on Windows, and SDL (built into
    /// the MonoGame native runtime) elsewhere. If neither is available, the message goes to stderr.
    /// </summary>
    public static class NativeMessageBox
    {
        public static DialogResult Show(string text) => Show(text, ApplicationInfo.ProductName);

        public static DialogResult Show(string text, string caption) => Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            try
            {
                return OperatingSystem.IsWindows() ? ShowWin32(text, caption, buttons, icon) : ShowSdl(text, caption, buttons, icon);
            }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException)
            {
                Console.Error.WriteLine("{0}\n\n{1}", caption, text);
                Trace.WriteLine(error);
                return buttons == MessageBoxButtons.OK ? DialogResult.OK : DialogResult.Cancel;
            }
        }

        #region Windows

        const uint MB_OK = 0x0;
        const uint MB_OKCANCEL = 0x1;
        const uint MB_ICONERROR = 0x10;
        const uint MB_ICONWARNING = 0x30;
        const uint MB_ICONINFORMATION = 0x40;
        const int IDOK = 1;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        static DialogResult ShowWin32(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            var type = (buttons == MessageBoxButtons.OKCancel ? MB_OKCANCEL : MB_OK) | icon switch
            {
                MessageBoxIcon.Error => MB_ICONERROR,
                MessageBoxIcon.Warning => MB_ICONWARNING,
                MessageBoxIcon.Information => MB_ICONINFORMATION,
                _ => 0u,
            };
            return MessageBoxW(IntPtr.Zero, text, caption, type) == IDOK ? DialogResult.OK : DialogResult.Cancel;
        }

        #endregion

        #region SDL2 (exported by MonoGame's libmgruntime)

        const string SdlLibrary = "mgruntime";
        const uint SDL_MESSAGEBOX_ERROR = 0x10;
        const uint SDL_MESSAGEBOX_WARNING = 0x20;
        const uint SDL_MESSAGEBOX_INFORMATION = 0x40;
        const uint SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT = 0x1;
        const uint SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT = 0x2;

        [StructLayout(LayoutKind.Sequential)]
        struct SDL_MessageBoxButtonData
        {
            public uint Flags;
            public int ButtonId;
            public IntPtr Text;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SDL_MessageBoxData
        {
            public uint Flags;
            public IntPtr Window;
            public IntPtr Title;
            public IntPtr Message;
            public int NumButtons;
            public IntPtr Buttons;
            public IntPtr ColorScheme;
        }

        [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl)]
        static extern int SDL_ShowMessageBox(ref SDL_MessageBoxData data, out int buttonId);

        static DialogResult ShowSdl(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            var allocations = new System.Collections.Generic.List<IntPtr>();
            IntPtr Utf8(string value)
            {
                var bytes = Encoding.UTF8.GetBytes(value + "\0");
                var pointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                allocations.Add(pointer);
                return pointer;
            }

            var buttonData = buttons == MessageBoxButtons.OKCancel
                ? new[]
                {
                    // SDL lists buttons right-to-left on some platforms; IDs identify them regardless of order.
                    new SDL_MessageBoxButtonData { Flags = SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, ButtonId = (int)DialogResult.Cancel, Text = Utf8("Cancel") },
                    new SDL_MessageBoxButtonData { Flags = SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, ButtonId = (int)DialogResult.OK, Text = Utf8("OK") },
                }
                : new[]
                {
                    new SDL_MessageBoxButtonData { Flags = SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT | SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, ButtonId = (int)DialogResult.OK, Text = Utf8("OK") },
                };
            var buttonSize = Marshal.SizeOf<SDL_MessageBoxButtonData>();
            var buttonArray = Marshal.AllocHGlobal(buttonSize * buttonData.Length);
            allocations.Add(buttonArray);
            for (var i = 0; i < buttonData.Length; i++)
                Marshal.StructureToPtr(buttonData[i], buttonArray + i * buttonSize, false);

            try
            {
                var data = new SDL_MessageBoxData
                {
                    Flags = icon switch
                    {
                        MessageBoxIcon.Error => SDL_MESSAGEBOX_ERROR,
                        MessageBoxIcon.Warning => SDL_MESSAGEBOX_WARNING,
                        _ => SDL_MESSAGEBOX_INFORMATION,
                    },
                    Title = Utf8(caption ?? ""),
                    Message = Utf8(text ?? ""),
                    NumButtons = buttonData.Length,
                    Buttons = buttonArray,
                };
                if (SDL_ShowMessageBox(ref data, out var buttonId) != 0)
                {
                    // No display available (e.g. headless); fall back to the console.
                    Console.Error.WriteLine("{0}\n\n{1}", caption, text);
                    return buttons == MessageBoxButtons.OK ? DialogResult.OK : DialogResult.Cancel;
                }
                return buttonId == (int)DialogResult.OK ? DialogResult.OK : DialogResult.Cancel;
            }
            finally
            {
                foreach (var pointer in allocations)
                    Marshal.FreeHGlobal(pointer);
            }
        }

        #endregion
    }
}
