using System.Net;
using System.Net.Sockets;
using NModbus;

// Minimal Modbus TCP slave for local testing: holding registers 0..3 random-walk around 500.
// After 60 s, register 0 drifts upward so the X-bar run rules fire.
var port = args.Length > 0 ? int.Parse(args[0]) : 1502;
var listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();
var slave = new ModbusFactory().CreateSlave(1);
var network = new ModbusFactory().CreateSlaveNetwork(listener);
network.AddSlave(slave);
_ = network.ListenAsync();
Console.WriteLine($"Modbus TCP slave on 127.0.0.1:{port}, unit 1. Ctrl+C to stop.");

var rng = new Random();
var start = DateTime.UtcNow;
var values = new double[] { 500, 500, 500, 500 };
while (true)
{
    var drift = (DateTime.UtcNow - start).TotalSeconds > 60 ? (DateTime.UtcNow - start).TotalSeconds - 60 : 0;
    for (var i = 0; i < values.Length; i++)
    {
        values[i] = 500 + (values[i] - 500) * 0.8 + (rng.NextDouble() - 0.5) * 20;
        slave.DataStore.HoldingRegisters.WritePoints((ushort)i, new[] { (ushort)Math.Clamp(values[i] + (i == 0 ? drift : 0), 0, 65535) });
    }
    await Task.Delay(100);
}
