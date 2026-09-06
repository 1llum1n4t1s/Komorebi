using Komorebi.Models;

namespace Komorebi.Tests.Models;

public class RefNameTests
{
    [Theory]
    [InlineData("main")]
    [InlineData("feature/日本語")]
    [InlineData("release/1.2.3")]
    [InlineData("fix/#123+test")]
    [InlineData("topic@user")]
    [InlineData("topic!value")]
    [InlineData("topic\"quoted")]
    public void ValidNames_AreAccepted(string name)
    {
        Assert.True(RefName.IsValidBranchName(name));
        Assert.True(RefName.IsValidTagName(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-option")]
    [InlineData("@")]
    [InlineData("a//b")]
    [InlineData("a/.hidden/b")]
    [InlineData("a/b.lock/c")]
    [InlineData("a..b")]
    [InlineData("a@{b}")]
    [InlineData("a.")]
    [InlineData("/a")]
    [InlineData("a/")]
    [InlineData("a b")]
    [InlineData("a\tb")]
    [InlineData("a\nb")]
    [InlineData("a\u007fb")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a[b")]
    [InlineData("a~b")]
    [InlineData("a^b")]
    public void InvalidNames_AreRejected(string? name)
    {
        Assert.False(RefName.IsValidBranchName(name));
        Assert.False(RefName.IsValidTagName(name));
    }

    [Fact]
    public void Head_IsReservedOnlyForBranches()
    {
        Assert.False(RefName.IsValidBranchName("HEAD"));
        Assert.True(RefName.IsValidTagName("HEAD"));
    }

    [Fact]
    public void PushDialog_ValidatesEmptyInvalidAndCorrectedInput()
    {
        var vm = new Komorebi.ViewModels.PushToNewBranch("origin");
        Assert.False(vm.Check());
        vm.BranchName = "topic..invalid";
        Assert.False(vm.Check());
        vm.BranchName = "topic/fixed";
        Assert.True(vm.Check());
    }
}
