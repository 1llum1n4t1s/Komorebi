using System.Security.Cryptography;

using Komorebi.Models;

namespace Komorebi.Tests.Models;

public class SSHKeyPairTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" test comment with spaces")]
    public void Fingerprint_DoesNotRequireSingleWordComment(string comment)
    {
        var path = Path.GetTempFileName();
        try
        {
            byte[] publicBlob = [0, 1, 2, 3, 4];
            File.WriteAllText(path, "ssh-ed25519 " + Convert.ToBase64String(publicBlob) + comment + "\n");
            var key = new SSHKeyPair(path + ".private", path);
            Assert.Equal("SHA256:" + Convert.ToBase64String(SHA256.HashData(publicBlob)).TrimEnd('='), key.Fingerprint);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
