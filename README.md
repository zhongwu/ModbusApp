# ModbusApp

WPF (.NET 10) app: polls Modbus TCP holding registers with NModbus, shows live values, and plots an X-bar chart
(ScottPlot) with Western Electric run-rule alarms. MVVM via CommunityToolkit.Mvvm.

## Run
1. Start a Modbus TCP simulator (e.g. ModRSsim2 or diagslave), or the bundled one: `dotnet run --project tools/ModbusSim` (127.0.0.1:1502, drifts after 60 s to trigger alarms).
2. Desktop: `dotnet run --project src/ModbusApp`. Browser: `dotnet run --project src/ModbusApp.Web --launch-profile http`, then open http://localhost:5202. Set host/port/unit/registers and click Connect.

Projects: `ModbusApp.Core` (NModbus poller + X-bar/run-rule logic, shared), `ModbusApp` (WPF), `ModbusApp.Web` (Blazor Server, SVG chart).

## X-bar chart
Samples from the chosen register are grouped into subgroups of n (2-10). The first 20 subgroup means set the baseline:
CL = mean of means, UCL/LCL = CL ± A2·R-bar. Afterwards each subgroup is checked for: beyond 3σ; 2 of 3 beyond 2σ;
4 of 5 beyond 1σ; 8 in a row on one side. Violations are marked red and logged. "Reset chart" restarts the baseline.
