using Avalonia.Input;

using Komorebi.Models;

using LiveChartsCore.SkiaSharpView.Avalonia;

using Velopack;

namespace Komorebi.Tests.Models;

public class LibraryCompatibilityTests
{
    [Fact]
    public void StatisticsChart_CanCreateWithCurrentAvalonia()
    {
        // データ集計だけでは検出できない、Avalonia の削除済み API 参照を検出する。
        var report = new StatisticsReport(StatisticsMode.ThisWeek, new DateTime(2026, 9, 7));
        report.Complete();
        var chart = new CartesianChart
        {
            Series = report.Series,
            XAxes = report.XAxes,
            YAxes = report.YAxes,
        };

        Assert.Same(report.Series, chart.Series);
        Assert.Same(report.XAxes, chart.XAxes);
        Assert.Same(report.YAxes, chart.YAxes);
        Assert.Contains(chart.GestureRecognizers, recognizer => recognizer is PinchGestureRecognizer);
    }

    [Fact]
    public void UpdateFeed_CanReadVersionAndPackageMetadata()
    {
        // Velopack のソース生成 JSON 経路はアセンブリ全体の保持を必要としない。
        var feed = VelopackAssetFeed.FromJson("""
            {"Assets":[{"PackageId":"Komorebi","Version":"1.2.3","Type":"Full",
            "FileName":"Komorebi-1.2.3-win-x64-full.nupkg","SHA1":"0123456789012345678901234567890123456789","Size":123}]}
            """);

        var asset = Assert.Single(feed.Assets);
        Assert.Equal("1.2.3", asset.Version.ToString());
        Assert.Equal(VelopackAssetType.Full, asset.Type);
        Assert.Equal("Komorebi-1.2.3-win-x64-full.nupkg", asset.FileName);
        Assert.Equal(123, asset.Size);
    }
}
