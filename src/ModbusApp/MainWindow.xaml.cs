using System.Windows;
using ModbusApp.ViewModels;
using ScottPlot;

namespace ModbusApp;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.ChartChanged += Redraw;
        Redraw();
    }

    private void Redraw()
    {
        var chart = _vm.Chart;
        Plot.Plot.Clear();
        Plot.Plot.Title("X-bar chart");
        Plot.Plot.XLabel("Subgroup");
        Plot.Plot.YLabel("Subgroup mean");

        if (chart.Means.Count > 0)
        {
            var xs = Enumerable.Range(1, chart.Means.Count).Select(i => (double)i).ToArray();
            var line = Plot.Plot.Add.Scatter(xs, chart.Means.ToArray());
            line.Color = Colors.SteelBlue;

            if (chart.Alarms.Count > 0)
            {
                var ax = chart.Alarms.Select(a => (double)(a.Subgroup + 1)).Distinct().ToArray();
                var ay = ax.Select(x => chart.Means[(int)x - 1]).ToArray();
                var marks = Plot.Plot.Add.Scatter(ax, ay);
                marks.LineWidth = 0;
                marks.MarkerSize = 12;
                marks.Color = Colors.Red;
            }
        }
        if (chart.LimitsLocked)
        {
            AddLimit(chart.CenterLine, Colors.Green, LinePattern.Solid);
            AddLimit(chart.Ucl, Colors.Red, LinePattern.Dashed);
            AddLimit(chart.Lcl, Colors.Red, LinePattern.Dashed);
        }
        Plot.Plot.Axes.AutoScale();
        Plot.Refresh();
    }

    private void AddLimit(double y, Color color, LinePattern pattern)
    {
        var l = Plot.Plot.Add.HorizontalLine(y);
        l.Color = color;
        l.LinePattern = pattern;
    }
}
