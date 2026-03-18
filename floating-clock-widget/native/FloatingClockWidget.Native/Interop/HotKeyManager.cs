using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FloatingClockWidget.Native.Interop;

[Flags]
public enum HotKeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    NoRepeat = 0x4000,
}

public sealed class HotKeyManager : IDisposable
{
    private const int WmHotKey = 0x0312;

    private readonly Window _window;
    private HwndSource? _source;
    private int _registeredId;

    public event EventHandler? HotKeyPressed;

    public HotKeyManager(Window window)
    {
        _window = window;
    }

    public bool TryRegister(int id, HotKeyModifiers modifiers, int virtualKey)
    {
        if (_source is null)
        {
            _source = (HwndSource)PresentationSource.FromVisual(_window)!;
            _source.AddHook(WndProc);
        }

        var handle = _source.Handle;
        var success = RegisterHotKey(handle, id, (uint)modifiers, (uint)virtualKey);
        if (success)
        {
            _registeredId = id;
        }

        return success;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == _registeredId)
        {
            HotKeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is not null)
        {
            if (_registeredId != 0)
            {
                UnregisterHotKey(_source.Handle, _registeredId);
                _registeredId = 0;
            }

            _source.RemoveHook(WndProc);
            _source = null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
