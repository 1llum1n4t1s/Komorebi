using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class GPGFormatTests
    {
        #region Constructor

        [Fact]
        public void Constructor_SetsAllProperties()
        {
            var format = new GPGFormat("Test", "test", "Description", "gpg", true);
            Assert.Equal("Test", format.Name);
            Assert.Equal("test", format.Value);
            Assert.Equal("Description", format.Desc);
            Assert.Equal("gpg", format.Program);
            Assert.True(format.NeedFindProgram);
        }

        #endregion

        #region Supported List

        [Fact]
        public void Supported_IsNotNull()
        {
            Assert.NotNull(GPGFormat.Supported);
        }

        [Fact]
        public void Supported_HasThreeEntries()
        {
            Assert.Equal(3, GPGFormat.Supported.Count);
        }

        [Fact]
        public void Supported_AllHaveNonEmptyName()
        {
            foreach (var format in GPGFormat.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(format.Name));
            }
        }

        [Fact]
        public void Supported_AllHaveNonEmptyValue()
        {
            foreach (var format in GPGFormat.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(format.Value));
            }
        }

        [Fact]
        public void Supported_AllHaveNonEmptyProgram()
        {
            foreach (var format in GPGFormat.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(format.Program));
            }
        }

        [Fact]
        public void Supported_AllValuesAreUnique()
        {
            var values = new HashSet<string>();
            foreach (var format in GPGFormat.Supported)
            {
                Assert.True(values.Add(format.Value),
                    $"Duplicate GPGFormat value: '{format.Value}'");
            }
        }

        #endregion

        #region Expected Entries

        [Fact]
        public void Supported_ContainsOpenPGP()
        {
            var format = GPGFormat.Supported.Find(f => f.Value == "openpgp");
            Assert.NotNull(format);
            Assert.Equal("OPENPGP", format.Name);
            Assert.Equal("gpg", format.Program);
            Assert.True(format.NeedFindProgram);
        }

        [Fact]
        public void Supported_ContainsX509()
        {
            var format = GPGFormat.Supported.Find(f => f.Value == "x509");
            Assert.NotNull(format);
            Assert.Equal("X.509", format.Name);
            Assert.Equal("gpgsm", format.Program);
            Assert.True(format.NeedFindProgram);
        }

        [Fact]
        public void Supported_ContainsSSH()
        {
            var format = GPGFormat.Supported.Find(f => f.Value == "ssh");
            Assert.NotNull(format);
            Assert.Equal("SSH", format.Name);
            Assert.Equal("ssh-keygen", format.Program);
            Assert.False(format.NeedFindProgram);
        }

        #endregion
    }
}
