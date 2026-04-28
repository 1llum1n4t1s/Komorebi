using Komorebi.ViewModels;

namespace Komorebi.Tests.ViewModels
{
    public class ExecuteCustomActionTests
    {
        [Fact]
        public void QuoteTemplateValue_ShShell_UsesSingleQuotesAndEscapesSubstitution()
        {
            var quoted = ExecuteCustomAction.QuoteTemplateValue("feature/$(whoami)'x", "sh");

            Assert.Equal("'feature/$(whoami)'\\''x'", quoted);
        }

        [Fact]
        public void QuoteTemplateValue_PowerShell_UsesSingleQuotes()
        {
            var quoted = ExecuteCustomAction.QuoteTemplateValue("feature/$(whoami)'x", "pwsh");

            Assert.Equal("'feature/$(whoami)''x'", quoted);
        }

        [Fact]
        public void CustomActionControlTextBox_CommandLineValue_EscapesOnlyUserValueInsideFormatter()
        {
            var parameter = new CustomActionControlTextBox("label", "", "feature/$(whoami)", "--branch ${VALUE}");

            Assert.Equal("--branch 'feature/$(whoami)'", parameter.GetCommandLineValue("sh"));
        }
    }
}
