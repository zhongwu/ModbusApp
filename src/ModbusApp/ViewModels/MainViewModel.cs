using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModbusApp.Services;

namespace ModbusApp.ViewModels;

public sealed partial class RegisterValue(int address) : ObservableObject
{
    public int Address { get; } = address;
    [ObservableProperty] private double _value;
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ModbusPoller _poller = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<RegisterValue> Registers { get; } = [];
    public ObservableCollection<string> AlarmLog { get; } = [];

    /// <summary>Raised on the UI thread whenever the chart needs to be redrawn.</summary>
    public event Action? ChartChanged;

    [ObservableProperty] private string _host = "127.0.0.1";
    [ObservableProperty] private int _port = 1502;
    [ObservableProperty] private byte _unitId = 1;
    [ObservableProperty] private ushort _startAddress;
    [ObservableProperty] private ushort _count = 4;
    [ObservableProperty] private int _intervalMs = 200;
    [ObservableProperty] private double _scale = 1;
    [ObservableProperty] private int _chartRegister;
    [ObservableProperty] private int _subgroupSize = 5;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDisconnected)), NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(DisconnectCommand))]
    private bool _isConnected;
    [ObservableProperty] private string _status = "Disconnected";
    [ObservableProperty] private bool _inAlarm;

    public bool IsDisconnected => !IsConnected;
    public XBarChart Chart { get; private set; } = new();

    [RelayCommand(CanExecute = nameof(IsDisconnected))]
    private async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            Status = $"Connecting to {Host}:{Port}…";
            await _poller.ConnectAsync(Host, Port, ct);
            IsConnected = true;
            Status = "Connected";
            ResetChart();
            Registers.Clear();
            for (var i = 0; i < Count; i++) Registers.Add(new RegisterValue(StartAddress + i));

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(IntervalMs));
            do { await PollOnceAsync(); } while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { Status = "Disconnected"; }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally
        {
            _poller.Dispose();
            IsConnected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private void Disconnect() => _cts?.Cancel();

    [RelayCommand]
    private void ResetChart()
    {
        Chart = new XBarChart(SubgroupSize);
        AlarmLog.Clear();
        InAlarm = false;
        ChartChanged?.Invoke();
    }

    private async Task PollOnceAsync()
    {
        var raw = await _poller.ReadHoldingAsync(UnitId, StartAddress, Count);
        for (var i = 0; i < raw.Length; i++) Registers[i].Value = raw[i] / Scale;

        var index = Math.Clamp(ChartRegister, 0, raw.Length - 1);
        var alarms = Chart.AddSample(raw[index] / Scale);
        foreach (var a in alarms)
            AlarmLog.Insert(0, $"{DateTime.Now:HH:mm:ss}  subgroup {a.Subgroup + 1}: {a.Rule}");
        if (alarms.Count > 0) InAlarm = true;
        ChartChanged?.Invoke();
    }
}
