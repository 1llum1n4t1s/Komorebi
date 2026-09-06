using System.Text.Json;

using Komorebi.AI;
using Komorebi.ViewModels;

namespace Komorebi.Tests.AI;

public class GenerationOptionsTests
{
    [Fact]
    public void ReasoningEffort_RoundTripsAndDefaultsToUnspecified()
    {
        Assert.Equal("unspecified", new Service().ReasoningEffortLevel);
        var preferences = new Preferences();
        preferences.OpenAIServices.Add(new Service { ReasoningEffortLevel = "low" });
        var json = JsonSerializer.Serialize(preferences, JsonCodeGen.Default.Preferences);
        var restored = JsonSerializer.Deserialize(json, JsonCodeGen.Default.Preferences);
        Assert.Equal("low", restored!.OpenAIServices.Single().ReasoningEffortLevel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("feature/issue-123")]
    public void Prompt_PreservesChangesAndIncludesOnlyAvailableBranch(string? branch)
    {
        var prompt = Agent.BuildUserMessage(new Service(), "/repo", "M file.txt", branch);
        Assert.Contains("M file.txt", prompt);
        if (branch == null)
            Assert.DoesNotContain("Current branch:", prompt);
        else
            Assert.Contains($"Current branch: {branch.Quoted()}", prompt);
    }
}
