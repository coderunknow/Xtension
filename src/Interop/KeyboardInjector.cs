using System;
using System.Runtime.InteropServices;

namespace Xtension
{
    /// <summary>
    /// Sends Ctrl+W to the foreground window with SendInput. Refuses to send while Shift, Alt or Win is held,
    /// because Ctrl+Shift+W closes the entire browser window instead of one tab.
    /// </summary>
    internal static class KeyboardInjector
    {
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;
        private const ushort VkShift = 0x10;
        private const ushort VkControl = 0x11;
        private const ushort VkMenu = 0x12;
        private const ushort VkLeftWin = 0x5B;
        private const ushort VkRightWin = 0x5C;
        private const ushort VkW = 0x57;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint inputCount, [In] INPUT[] inputs, int structSize);

        #pragma warning disable 0649 // Native structs are filled by the marshaller and by union writes.
        // Win32 INPUT: a DWORD type followed by a union whose largest member is MOUSEINPUT.
        // That union sets the size: 40 bytes on x64 and 28 bytes on x86. Declaring only KEYBDINPUT would make
        // the structure too small on x64 and SendInput would fail with ERROR_INVALID_PARAMETER.
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION union;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mouse;
            [FieldOffset(0)] public KEYBDINPUT keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }
        #pragma warning restore 0649

        /// <summary>Size SendInput expects for one INPUT element on this process architecture.</summary>
        public static int InputStructureSize
        {
            get { return Marshal.SizeOf(typeof(INPUT)); }
        }

        public static bool BlockingModifierDown()
        {
            return IsDown(VkShift) || IsDown(VkMenu) || IsDown(VkLeftWin) || IsDown(VkRightWin);
        }

        /// <summary>Sends Ctrl down, W down, W up, Ctrl up. On partial failure, releases both keys and reports the error.</summary>
        public static bool SendControlW(out string failure)
        {
            failure = null;
            INPUT[] inputs = new INPUT[]
            {
                KeyInput(VkControl, false),
                KeyInput(VkW, false),
                KeyInput(VkW, true),
                KeyInput(VkControl, true)
            };

            int size = InputStructureSize;
            uint sent = SendInput((uint)inputs.Length, inputs, size);
            if (sent == (uint)inputs.Length) return true;

            int error = Marshal.GetLastWin32Error();
            failure = string.Format(
                "SendInput delivered {0}/{1} key events (structure size {2}; Win32 error {3}: {4}).",
                sent, inputs.Length, size, error, new System.ComponentModel.Win32Exception(error).Message);

            // A partial delivery can leave a key logically down. Release W and Ctrl so the user's keyboard is not stuck.
            INPUT[] releases = new INPUT[] { KeyInput(VkW, true), KeyInput(VkControl, true) };
            SendInput((uint)releases.Length, releases, size);
            return false;
        }

        private static bool IsDown(int virtualKey)
        {
            return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static INPUT KeyInput(ushort key, bool keyUp)
        {
            INPUT input = new INPUT();
            input.type = InputKeyboard;
            input.union.keyboard.wVk = key;
            input.union.keyboard.wScan = 0;
            input.union.keyboard.dwFlags = keyUp ? KeyEventKeyUp : 0u;
            input.union.keyboard.time = 0;
            input.union.keyboard.dwExtraInfo = UIntPtr.Zero;
            return input;
        }
    }
}
