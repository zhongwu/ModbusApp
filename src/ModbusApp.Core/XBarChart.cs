namespace ModbusApp.Services;

public sealed record Alarm(int Subgroup, string Rule);

/// <summary>
/// X-bar chart: groups samples into subgroups of size n, locks control limits from the first
/// <see cref="BaselineSubgroups"/> subgroups (CL = mean of means, UCL/LCL = CL ± A2·R-bar),
/// then checks Western Electric run rules on every new subgroup mean.
/// </summary>
public sealed class XBarChart
{
    // A2 factors for subgroup sizes 2..10.
    private static readonly double[] A2 = [0, 0, 1.880, 1.023, 0.729, 0.577, 0.483, 0.419, 0.373, 0.337, 0.308];

    private readonly List<double> _current = [];
    private readonly List<double> _ranges = [];

    public XBarChart(int subgroupSize = 5, int baselineSubgroups = 20)
    {
        if (subgroupSize is < 2 or > 10) throw new ArgumentOutOfRangeException(nameof(subgroupSize), "Subgroup size must be 2..10.");
        SubgroupSize = subgroupSize;
        BaselineSubgroups = baselineSubgroups;
    }

    public int SubgroupSize { get; }
    public int BaselineSubgroups { get; }
    public List<double> Means { get; } = [];
    public List<Alarm> Alarms { get; } = [];

    public bool LimitsLocked => Means.Count >= BaselineSubgroups;
    public double CenterLine { get; private set; }
    public double Ucl { get; private set; }
    public double Lcl { get; private set; }
    public double Sigma => (Ucl - CenterLine) / 3;

    /// <summary>Adds a sample. Returns the alarms raised by the subgroup it completed (empty otherwise).</summary>
    public IReadOnlyList<Alarm> AddSample(double value)
    {
        _current.Add(value);
        if (_current.Count < SubgroupSize) return [];

        Means.Add(_current.Average());
        _ranges.Add(_current.Max() - _current.Min());
        _current.Clear();

        if (Means.Count == BaselineSubgroups) LockLimits();
        return Means.Count > BaselineSubgroups ? Evaluate() : [];
    }

    private void LockLimits()
    {
        CenterLine = Means.Average();
        var width = A2[SubgroupSize] * _ranges.Average();
        Ucl = CenterLine + width;
        Lcl = CenterLine - width;
    }

    private List<Alarm> Evaluate()
    {
        var i = Means.Count - 1;
        var raised = new List<Alarm>();
        if (Sigma <= 0) return raised;
        var z = (Means[i] - CenterLine) / Sigma;

        if (Math.Abs(z) > 3) raised.Add(new(i, "Rule 1: point beyond 3σ"));
        if (CountBeyond(3, 2) >= 2) raised.Add(new(i, "Rule 2: 2 of 3 beyond 2σ (same side)"));
        if (CountBeyond(5, 1) >= 4) raised.Add(new(i, "Rule 3: 4 of 5 beyond 1σ (same side)"));
        if (i >= 7 && z != 0 && Enumerable.Range(i - 7, 8).All(k => Math.Sign(Means[k] - CenterLine) == Math.Sign(z)))
            raised.Add(new(i, "Rule 4: 8 in a row on one side"));

        Alarms.AddRange(raised);
        return raised;
    }

    /// <summary>
    /// Count of the last <paramref name="window"/> points beyond <paramref name="sigmas"/> on the newest point's side.
    /// Returns 0 unless the newest point is itself beyond the threshold, so each violation fires once.
    /// </summary>
    private int CountBeyond(int window, double sigmas)
    {
        var i = Means.Count - 1;
        var side = Math.Sign(Means[i] - CenterLine);
        if (side == 0 || i < window - 1) return 0;
        if (side * (Means[i] - CenterLine) / Sigma <= sigmas) return 0;
        var n = 0;
        for (var k = i - window + 1; k <= i; k++)
            if (side * (Means[k] - CenterLine) / Sigma > sigmas) n++;
        return n;
    }
}
