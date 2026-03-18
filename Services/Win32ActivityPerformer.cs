using System.Runtime.InteropServices;
using System.Threading;
using Activer.Core.Models;
using Activer.Core.Services;

namespace Activer.Services;

public sealed class Win32ActivityPerformer : IActivityPerformer
{
    private const int InputMouse = 0;
    private const int InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventAbsolute = 0x8000;
    private const uint MouseEventVirtualDesk = 0x4000;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public ActivityExecutionResult Perform(ActivityExecutionRequest request)
    {
        if (!GetCursorPos(out var original))
        {
            return new ActivityExecutionResult(false, 0, 0);
        }

        SmoothMove(original.X, original.Y, original.X + request.OffsetX, original.Y + request.OffsetY, 10, 20);
        SmoothMove(original.X + request.OffsetX, original.Y + request.OffsetY, original.X, original.Y, 10, 20);

        SendKeyboardInput(request.VirtualKeyCode, isKeyUp: false);
        Thread.Sleep(75);
        SendKeyboardInput(request.VirtualKeyCode, isKeyUp: true);

        return new ActivityExecutionResult(true, original.X, original.Y);
    }

    private static void SmoothMove(int startX, int startY, int endX, int endY, int steps, int delayMs)
    {
        for (var i = 1; i <= steps; i++)
        {
            var x = startX + ((endX - startX) * i / steps);
            var y = startY + ((endY - startY) * i / steps);
            SendMouseMove(x, y);
            Thread.Sleep(delayMs);
        }
    }

    private static void SendMouseMove(int x, int y)
    {
        var input = new INPUT
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = NormalizeToAbsoluteX(x),
                    dy = NormalizeToAbsoluteY(y),
                    dwFlags = MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesk,
                },
            },
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendKeyboardInput(byte virtualKeyCode, bool isKeyUp)
    {
        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKeyCode,
                    dwFlags = isKeyUp ? KeyEventKeyUp : 0,
                },
            },
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static int NormalizeToAbsoluteX(int x)
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var width = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen) - 1);
        return (int)Math.Round((x - left) * 65535d / width);
    }

    private static int NormalizeToAbsoluteY(int y)
    {
        var top = GetSystemMetrics(SmYVirtualScreen);
        var height = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen) - 1);
        return (int)Math.Round((y - top) * 65535d / height);
    }
}
