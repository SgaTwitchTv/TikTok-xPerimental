using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AutoClicker;

internal static class NativeInput
{
    internal const int HotkeyId = 0x6A31;
    internal const int WmHotkey = 0x0312;
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    // INPUT's union must also fit KEYBDINPUT and HARDWAREINPUT on x86/ARM64/x64.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public HardwareInput Hardware;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X, Y;
        public uint MouseData, Flags, Time;
        public nuint ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Key, Scan;
        public uint Flags, Time;
        public nuint ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput { public uint Message; public ushort ParamLow, ParamHigh; }

    internal static void Click()
    {
        Input[] inputs = [
            new() { Data = new() { Mouse = new() { Flags = 0x0002 } } },
            new() { Data = new() { Mouse = new() { Flags = 0x0004 } } }
        ];
        uint sent = SendInput(2, inputs, Marshal.SizeOf<Input>());
        if (sent == 2) return;
        int error = Marshal.GetLastWin32Error();
        // A partial insertion must not leave the button held down.
        if (sent == 1) SendInput(1, [inputs[1]], Marshal.SizeOf<Input>());
        throw new Win32Exception(error, "Windows nie przyjął kliknięcia. Sprawdź uprawnienia okna docelowego.");
    }
}
