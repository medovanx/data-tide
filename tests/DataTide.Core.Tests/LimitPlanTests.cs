using DataTide.Core.Limiting;

namespace DataTide.Core.Tests;

public class LimitPlanTests
{
    static readonly LimitRule Browser = new() { ExePath = @"C:\b\browser.exe", DownBytesPerSecond = 1000 };
    static readonly LimitRule Paused = new() { ExePath = @"C:\p\paused.exe", DownBytesPerSecond = 1000, Enabled = false };

    [Fact]
    public void NothingWhenAllOff()
    {
        var p = LimitPlan.Build(false, [Browser], false, [], 1, 1, false, []);
        Assert.False(p.Active);
    }

    [Fact]
    public void SavedRulesOnlyWhenLimitsOnAndEnabled()
    {
        var p = LimitPlan.Build(true, [Browser, Paused], false, [], 1, 1, false, []);
        Assert.Single(p.Rules);
        Assert.Equal(1000, p.Rules[0].DownBytesPerSecond);
        Assert.Null(p.Others);
    }

    [Fact]
    public void MeteredBlockWinsOverSavedRuleAndWorksWithLimitsOff()
    {
        var p = LimitPlan.Build(false, [Browser], false, [], 1, 1, true, [@"c:\B\BROWSER.EXE"]);
        Assert.Single(p.Rules);
        Assert.True(p.Rules[0].Block);
        var q = LimitPlan.Build(true, [Browser], false, [], 1, 1, true, [@"C:\b\browser.exe"]);
        Assert.Single(q.Rules);
        Assert.True(q.Rules[0].Block);
    }

    [Fact]
    public void GameModeAddsSharedLimitExceptGamesAndSelf()
    {
        var p = LimitPlan.Build(false, [], true, [@"C:\g\game.exe"], 512, 128, false, [], @"C:\dt\DataTide.exe");
        Assert.True(p.Active);
        Assert.Equal(512, p.Others!.DownBytesPerSecond);
        Assert.Equal(128, p.Others.UpBytesPerSecond);
        Assert.Contains(@"C:\g\game.exe", p.Others.Except);
        Assert.Contains(@"C:\dt\DataTide.exe", p.Others.Except);
        Assert.Null(LimitPlan.Build(false, [], false, [@"C:\g\game.exe"], 512, 128, false, []).Others);
    }

    [Fact]
    public void SavedRulesAreCopiesNotTheSameObjects()
    {
        var p = LimitPlan.Build(true, [Browser], false, [], 1, 1, false, []);
        Assert.NotSame(Browser, p.Rules[0]);
    }
}
