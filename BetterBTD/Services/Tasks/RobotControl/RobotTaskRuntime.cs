using BetterBTD.Core.RobotControl;
using BetterBTD.Models.RobotControl;

namespace BetterBTD.Services.Tasks.RobotControl;

public sealed class RobotTaskRuntime
{
    private static readonly Lazy<RobotTaskRuntime> InstanceHolder = new(() => new RobotTaskRuntime());

    private readonly object _syncRoot = new();
    private readonly RobotTaskCoordinator _coordinator;
    private readonly RobotTaskHttpServer _httpServer;

    private CancellationTokenSource? _cancellationSource;
    private Task? _uiAutomationLoopTask;
    private bool _isRunning;

    public RobotTaskRuntime()
        : this(RobotTaskCoordinator.Instance, new RobotTaskHttpServer(RobotTaskCoordinator.Instance))
    {
    }

    internal RobotTaskRuntime(RobotTaskCoordinator coordinator, RobotTaskHttpServer httpServer)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _httpServer = httpServer ?? throw new ArgumentNullException(nameof(httpServer));
    }

    public static RobotTaskRuntime Instance => InstanceHolder.Value;

    public event EventHandler<RobotTaskStatusSnapshot>? StatusChanged
    {
        add => _coordinator.StatusChanged += value;
        remove => _coordinator.StatusChanged -= value;
    }

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

    public RobotTaskStatusSnapshot CurrentStatus => _coordinator.GetStatusSnapshot();

    public async Task StartAsync(
        RobotTaskRuntimeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RobotTaskRuntimeOptions();

        lock (_syncRoot)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Robot task runtime is already running.");
            }

            _isRunning = true;
            _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        try
        {
            _coordinator.Start(options.ListenUrl);
            await _httpServer.StartAsync(options.ListenUrl, cancellationToken).ConfigureAwait(false);

            var runtimeToken = GetRuntimeToken();
            _uiAutomationLoopTask = Task.Run(
                () => RunUiAutomationLoopAsync(options.UiAutomationPollIntervalMs, runtimeToken),
                CancellationToken.None);
        }
        catch
        {
            await StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        Task? uiAutomationLoopTask;
        CancellationTokenSource? cancellationSource;

        lock (_syncRoot)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            cancellationSource = _cancellationSource;
            uiAutomationLoopTask = _uiAutomationLoopTask;
            _cancellationSource = null;
            _uiAutomationLoopTask = null;
        }

        cancellationSource?.Cancel();

        await _httpServer.StopAsync().ConfigureAwait(false);

        if (uiAutomationLoopTask is not null)
        {
            try
            {
                await uiAutomationLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _coordinator.Stop();
        cancellationSource?.Dispose();
    }

    private async Task RunUiAutomationLoopAsync(int pollIntervalMs, CancellationToken cancellationToken)
    {
        var delayMs = Math.Clamp(pollIntervalMs <= 0 ? 300 : pollIntervalMs, 100, 5000);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _coordinator.TryRunUiAutomationAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private CancellationToken GetRuntimeToken()
    {
        lock (_syncRoot)
        {
            return _cancellationSource?.Token ?? CancellationToken.None;
        }
    }
}
