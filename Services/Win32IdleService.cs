using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Activer.Core.Services;

namespace Activer.Services;

public sealed class Win32IdleService : IIdleService, IDisposable
{
    private const int HcAction = 0;
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int LlkhfInjected = 0x10;
    private const int LlkhfLowerIlInjected = 0x02;
    private const int LlmhfInjected = 0x00000001;

    private readonly LowLevelProc keyboardProc;
    private readonly LowLevelProc mouseProc;
    private IntPtr keyboardHook;
    private IntPtr mouseHook;
    private long lastPhysicalInputTick;

    public Win32IdleService()
    {
        lastPhysicalInputTick = GetSystemLastInputTick();
        keyboardProc = KeyboardHookCallback;
        mouseProc = MouseHookCallback;
        keyboardHook = SetLowLevelHook(WhKeyboardLl, keyboardProc);
        mouseHook = SetLowLevelHook(WhMouseLl, mouseProc);
    }

    public int GetIdleSeconds()
    {
        var currentTick = Environment.TickCount64;
        var lastTick = HasInstalledHooks ? Interlocked.Read(ref lastPhysicalInputTick) : GetSystemLastInputTick();
        var idleMilliseconds = Math.Max(0, currentTick - lastTick);
        return (int)(idleMilliseconds / 1000);
    }

    public void Dispose()
    {
        if (keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(keyboardHook);
            keyboardHook = IntPtr.Zero;
        }

        if (mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(mouseHook);
            mouseHook = IntPtr.Zero;
        }
    }

    private bool HasInstalledHooks => keyboardHook != IntPtr.Zero || mouseHook != IntPtr.Zero;

    private static long GetSystemLastInputTick()
    {
        var lastInput = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref lastInput))
        {
            return Environment.TickCount64;
        }

        var currentTick64 = Environment.TickCount64;
        var currentTick32 = unchecked((uint)Environment.TickCount);
        var delta = unchecked(currentTick32 - lastInput.dwTime);
        return currentTick64 - delta;
    }

    private static IntPtr SetLowLevelHook(int hookId, LowLevelProc callback)
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = module is null ? IntPtr.Zero : GetModuleHandle(module.ModuleName);
        return SetWindowsHookEx(hookId, callback, moduleHandle, 0);
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= HcAction && lParam != IntPtr.Zero)
        {
            var keyboardData = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if ((keyboardData.flags & (LlkhfInjected | LlkhfLowerIlInjected)) == 0)
            {
                Interlocked.Exchange(ref lastPhysicalInputTick, Environment.TickCount64);
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= HcAction && lParam != IntPtr.Zero)
        {
            var mouseData = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if ((mouseData.flags & LlmhfInjected) == 0)
            {
                Interlocked.Exchange(ref lastPhysicalInputTick, Environment.TickCount64);
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
