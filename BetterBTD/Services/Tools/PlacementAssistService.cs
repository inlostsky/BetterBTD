using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BetterBTD.Services.Tasks.Input;

namespace BetterBTD.Services.Tools;

public sealed class PlacementAssistService
{
    private const int StepPixels = 1;
    private const int LongPressThresholdMilliseconds = 500;
    private const int RepeatIntervalMilliseconds = 16;
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;

    private static readonly Lazy<PlacementAssistService> InstanceHolder = new(() => new PlacementAssistService());

    private readonly object _syncRoot = new();
    private readonly ScriptInputSimulationService _inputSimulationService;
    private readonly Dictionary<int, DateTimeOffset> _pressedDirections = [];
    private readonly LowLevelKeyboardProc _keyboardProc;

    private Timer? _repeatTimer;
    private IntPtr _hookHandle;
    private bool _isRunning;
    private string? _lastError;

    private PlacementAssistService()
        : this(ScriptInputSimulationService.Instance)
    {
    }

    internal PlacementAssistService(ScriptInputSimulationService inputSimulationService)
    {
        _inputSimulationService = inputSimulationService ?? throw new ArgumentNullException(nameof(inputSimulationService));
        _keyboardProc = KeyboardHookCallback;
    }

    public static PlacementAssistService Instance => InstanceHolder.Value;

    public event EventHandler? StateChanged;

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _isRunning;
            }
        }
    }

    public string? LastError
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastError;
            }
        }
    }

    public bool Start()
    {
        lock (_syncRoot)
        {
            if (_isRunning)
            {
                return true;
            }

            _lastError = null;
            _pressedDirections.Clear();

            try
            {
                _hookHandle = InstallKeyboardHook();
                _isRunning = true;
            }
            catch (Exception ex)
            {
                _hookHandle = IntPtr.Zero;
                _isRunning = false;
                _lastError = ex.Message;
            }
        }

        OnStateChanged();
        return IsRunning;
    }

    public void Stop()
    {
        StopCore(raiseStateChanged: true);
    }

    public void Shutdown()
    {
        StopCore(raiseStateChanged: false);
    }

    private void StopCore(bool raiseStateChanged)
    {
        IntPtr hookToRelease;
        Timer? timerToDispose;

        lock (_syncRoot)
        {
            if (!_isRunning && _hookHandle == IntPtr.Zero && _repeatTimer is null)
            {
                return;
            }

            _isRunning = false;
            _pressedDirections.Clear();
            hookToRelease = _hookHandle;
            timerToDispose = _repeatTimer;
            _hookHandle = IntPtr.Zero;
            _repeatTimer = null;
        }

        timerToDispose?.Dispose();

        if (hookToRelease != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(hookToRelease);
        }

        if (raiseStateChanged)
        {
            OnStateChanged();
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp)
            {
                var hookData = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
                if (IsDirectionKey(hookData.VkCode))
                {
                    if (message is WmKeyDown or WmSysKeyDown)
                    {
                        HandleDirectionKeyDown(hookData.VkCode);
                    }
                    else
                    {
                        HandleDirectionKeyUp(hookData.VkCode);
                    }

                    return new IntPtr(1);
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void HandleDirectionKeyDown(int virtualKey)
    {
        bool shouldMoveImmediately;
        bool shouldStartRepeatTimer;

        lock (_syncRoot)
        {
            if (!_isRunning)
            {
                return;
            }

            shouldStartRepeatTimer = _pressedDirections.Count == 0;
            shouldMoveImmediately = !_pressedDirections.ContainsKey(virtualKey);
            if (shouldMoveImmediately)
            {
                _pressedDirections[virtualKey] = DateTimeOffset.UtcNow;
                if (shouldStartRepeatTimer)
                {
                    EnsureRepeatTimer(LongPressThresholdMilliseconds);
                }
            }
        }

        if (shouldMoveImmediately)
        {
            MovePointer(GetDirectionDelta(virtualKey));
        }
    }

    private void HandleDirectionKeyUp(int virtualKey)
    {
        lock (_syncRoot)
        {
            _pressedDirections.Remove(virtualKey);
            if (_pressedDirections.Count == 0)
            {
                _repeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
    }

    private void EnsureRepeatTimer(int dueTimeMilliseconds)
    {
        if (_repeatTimer is null)
        {
            _repeatTimer = new Timer(
                OnRepeatTimerTick,
                null,
                dueTimeMilliseconds,
                RepeatIntervalMilliseconds);
            return;
        }

        _repeatTimer.Change(dueTimeMilliseconds, RepeatIntervalMilliseconds);
    }

    private void OnRepeatTimerTick(object? state)
    {
        _ = state;

        (int DeltaX, int DeltaY) delta;
        lock (_syncRoot)
        {
            if (!_isRunning || _pressedDirections.Count == 0)
            {
                return;
            }

            delta = GetCurrentDelta(DateTimeOffset.UtcNow);
        }

        MovePointer(delta);
    }

    private void MovePointer((int DeltaX, int DeltaY) delta)
    {
        if (delta is { DeltaX: 0, DeltaY: 0 })
        {
            return;
        }

        try
        {
            _inputSimulationService.MoveMouseBy(delta.DeltaX, delta.DeltaY);
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                _lastError = ex.Message;
            }

            Stop();
        }
    }

    private (int DeltaX, int DeltaY) GetCurrentDelta(DateTimeOffset now)
    {
        var deltaX = 0;
        var deltaY = 0;

        foreach (var (direction, pressedAt) in _pressedDirections)
        {
            if ((now - pressedAt).TotalMilliseconds < LongPressThresholdMilliseconds)
            {
                continue;
            }

            var delta = GetDirectionDelta(direction);
            deltaX += delta.DeltaX;
            deltaY += delta.DeltaY;
        }

        return (Math.Clamp(deltaX, -StepPixels, StepPixels), Math.Clamp(deltaY, -StepPixels, StepPixels));
    }

    private static (int DeltaX, int DeltaY) GetDirectionDelta(int virtualKey)
    {
        return virtualKey switch
        {
            VkLeft => (-StepPixels, 0),
            VkRight => (StepPixels, 0),
            VkUp => (0, -StepPixels),
            VkDown => (0, StepPixels),
            _ => (0, 0)
        };
    }

    private static IntPtr InstallKeyboardHook()
    {
        using var process = Process.GetCurrentProcess();
        var module = process.MainModule;
        var moduleHandle = module?.ModuleName is { Length: > 0 } moduleName
            ? GetModuleHandle(moduleName)
            : IntPtr.Zero;

        var hookHandle = SetWindowsHookEx(WhKeyboardLl, Instance._keyboardProc, moduleHandle, 0);
        if (hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install placement assist keyboard hook.");
        }

        return hookHandle;
    }

    private static bool IsDirectionKey(int virtualKey)
    {
        return virtualKey is VkLeft or VkRight or VkUp or VkDown;
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Kbdllhookstruct
    {
        public readonly int VkCode;
        public readonly int ScanCode;
        public readonly int Flags;
        public readonly int Time;
        public readonly IntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
