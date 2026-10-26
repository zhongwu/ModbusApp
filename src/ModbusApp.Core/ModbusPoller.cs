using System.Net.Sockets;
using NModbus;

namespace ModbusApp.Services;

public sealed class ModbusPoller : IDisposable
{
    private TcpClient? _client;
    private IModbusMaster? _master;

    public async Task ConnectAsync(string host, int port, CancellationToken ct)
    {
        Dispose();
        _client = new TcpClient();
        await _client.ConnectAsync(host, port, ct);
        _master = new ModbusFactory().CreateMaster(_client);
        _master.Transport.ReadTimeout = 2000;
    }

    public Task<ushort[]> ReadHoldingAsync(byte unitId, ushort start, ushort count) =>
        (_master ?? throw new InvalidOperationException("Not connected.")).ReadHoldingRegistersAsync(unitId, start, count);

    public void Dispose()
    {
        _master?.Dispose();
        _client?.Dispose();
        _master = null;
        _client = null;
    }
}
