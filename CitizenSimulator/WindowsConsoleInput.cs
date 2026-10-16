using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using CitizenSimulator.Abstractions.Input;
using CitizenSimulator.Engine.Input;
namespace CitizenSimulator;

// Reads native key up/down records and Unicode text from the same input stream.
internal sealed class WindowsConsoleInput : IDisposable
{
    private readonly nint _handle;
    private readonly uint _originalMode;
    private readonly InputDispatcher _input;
    private readonly StringBuilder _line = new();
    private readonly Record[] _records = new Record[64];
    private WindowsConsoleInput(nint handle, uint mode, InputDispatcher input) { _handle = handle; _originalMode = mode; _input = input; }
    public static WindowsConsoleInput? TryCreate(InputDispatcher input)
    {
        // currently only implmentation is for windows can add more but im lazy
        if (!OperatingSystem.IsWindows() || Console.IsInputRedirected) return null;
        var handle = GetStdHandle(-10);
        
        // read current console settings so we can keep them + needed cahnges
        if (!GetConsoleMode(handle, out var mode)) return null;
        
        // enable windows input, extended flags(to disable quick edit) disable line input, echo input, quick edit and virtual terminal input
        // edit: i also disable mouse input as it caused problems in my ide
        if (!SetConsoleMode(handle, (mode | 0x88u) & ~0x256u))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return new WindowsConsoleInput(handle, mode, input);
    }
    public void Poll()
    {
        //get input buffer size
        if (!GetNumberOfConsoleInputEvents(_handle, out var available))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (available == 0) return;
        if (!ReadConsoleInputW(_handle, _records, Math.Min(available, 64u), out var count)) // read max 64
            throw new Win32Exception(Marshal.GetLastWin32Error());
        for (var i = 0; i < count; i++)
        {
            var record = _records[i];
            if (record.Type == 0x10 && record.Down == 0) { _input.Reset(); continue; }
            if (record.Type != 1) continue;
            var modifiers = KeyModifiers.None;
            if ((record.Controls & 0x10) != 0) modifiers |= KeyModifiers.Shift;
            if ((record.Controls & 0x0c) != 0) modifiers |= KeyModifiers.Control;
            if ((record.Controls & 0x03) != 0) modifiers |= KeyModifiers.Alt;
            var key = MapKey(record.VirtualKey);
            if (record.Down == 0) { _input.Release(key, modifiers); continue; }
            _input.Press(key, modifiers);
            for (var repeat = 0; repeat < record.Repeat; repeat++)
            {
                if (record.VirtualKey == 13)
                {
                    Console.WriteLine();
                    _input.SubmitLine(_line.ToString());
                    _line.Clear();
                }
                else if (record.VirtualKey == 8 && _line.Length > 0)
                { _line.Length--; Console.Write("\b \b"); }
                else if (!char.IsControl(record.Character) && record.Character != '\0')
                { _line.Append(record.Character); Console.Write(record.Character); }
            }
        }
    }
    private static InputKey MapKey(ushort key) => key switch
    {
        13 => InputKey.Enter, 27 => InputKey.Escape, 32 => InputKey.Space,
        37 => InputKey.Left, 38 => InputKey.Up, 39 => InputKey.Right, 40 => InputKey.Down,
        8 => InputKey.Backspace, 9 => InputKey.Tab,
        >= 65 and <= 90 => InputKey.A + (key - 65),
        >= 112 and <= 123 => InputKey.F1 + (key - 112),
        16 => InputKey.Shift, 17 => InputKey.Control, 18 => InputKey.Alt,
        _ => InputKey.Unknown
    };
    public void Dispose() { SetConsoleMode(_handle, _originalMode); }
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 20)]
    private struct Record
    {
        // make sure each field matches windows memory layout ot things will corrupt
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(4)] public int Down;
        [FieldOffset(8)] public ushort Repeat;
        [FieldOffset(10)] public ushort VirtualKey;
        [FieldOffset(14)] public char Character;
        [FieldOffset(16)] public uint Controls;
    }
    [DllImport("kernel32.dll")] private static extern nint GetStdHandle(int identifier);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetConsoleMode(nint handle, out uint mode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleMode(nint handle, uint mode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNumberOfConsoleInputEvents(nint handle, out uint count);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadConsoleInputW(nint handle, [Out] Record[] records, uint length, out uint count);
}
