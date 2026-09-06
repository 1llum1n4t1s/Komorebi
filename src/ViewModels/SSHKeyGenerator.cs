#nullable disable warnings
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

public class SSHKeyGenerator : ObservableValidator
{
    public Models.SSHKeyType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    [Required(ErrorMessage = "Name is required.")]
    [RegularExpression(@"^[a-zA-Z0-9_\-]+$", ErrorMessage = "Name can only contain letters, numbers, underscores, and hyphens.")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, true);
    }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value, true);
    }

    public bool UsePassphrase
    {
        get => _usePassphrase;
        set
        {
            if (SetProperty(ref _usePassphrase, value))
            {
                ValidateProperty(_passphrase, nameof(Passphrase));
                ValidateProperty(_confirmedPassphrase, nameof(ConfirmedPassphrase));
            }
        }
    }

    [CustomValidation(typeof(SSHKeyGenerator), nameof(ValidatePassphrase))]
    public string Passphrase
    {
        get => _passphrase;
        set
        {
            if (SetProperty(ref _passphrase, value, true))
                ValidateProperty(_confirmedPassphrase, nameof(ConfirmedPassphrase));
        }
    }

    [CustomValidation(typeof(SSHKeyGenerator), nameof(ValidateConfirmedPassphrase))]
    public string ConfirmedPassphrase
    {
        get => _confirmedPassphrase;
        set => SetProperty(ref _confirmedPassphrase, value, true);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public static ValidationResult ValidatePassphrase(string password, ValidationContext context)
    {
        var instance = (SSHKeyGenerator)context.ObjectInstance;
        if (!instance.UsePassphrase)
            return ValidationResult.Success;

        if (string.IsNullOrEmpty(password))
            return new ValidationResult("Passphrase is required!");

        return ValidationResult.Success;
    }

    public static ValidationResult ValidateConfirmedPassphrase(string confirmedPassword, ValidationContext context)
    {
        var instance = (SSHKeyGenerator)context.ObjectInstance;
        if (!instance.UsePassphrase)
            return ValidationResult.Success;

        if (confirmedPassword != instance.Passphrase)
            return new ValidationResult("Passphrase and confirmation do not match!");

        return ValidationResult.Success;
    }

    public async Task<Models.SSHKeyPair> RunAsync()
    {
        ErrorMessage = string.Empty;
        ValidateAllProperties();
        if (HasErrors)
            return null;

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        var keyFile = Path.Combine(dir, Name);
        var publicKeyFile = keyFile + ".pub";
        // 上流との差分: 既存鍵の上書き確認待ちや誤った成功判定を防ぐ。
        if (File.Exists(keyFile) || File.Exists(publicKeyFile))
        {
            ErrorMessage = "A key with this name already exists.";
            return null;
        }

        try
        {
            Directory.CreateDirectory(dir);
            var start = new ProcessStartInfo("ssh-keygen")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in new[] { "-q", "-t", Type?.Name == "RSA" ? "rsa" : "ed25519" })
                start.ArgumentList.Add(argument);
            if (Type?.Name == "RSA")
            {
                start.ArgumentList.Add("-b");
                start.ArgumentList.Add("4096");
            }
            foreach (var argument in new[] { "-N", UsePassphrase ? Passphrase : string.Empty, "-C", Email, "-f", keyFile })
                start.ArgumentList.Add(argument);

            using var process = Process.Start(start);
            if (process == null)
                throw new InvalidOperationException("Could not start ssh-keygen.");
            process.StandardInput.Close();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            await stdout;
            var error = await stderr;
            if (process.ExitCode != 0 || !File.Exists(keyFile) || !File.Exists(publicKeyFile))
            {
                ErrorMessage = "Failed to generate SSH key: " + error;
                return null;
            }

            return new Models.SSHKeyPair(keyFile, publicKeyFile);
        }
        catch (Exception e)
        {
            ErrorMessage = "Failed to generate SSH key: " + e.Message;
            return null;
        }
        finally
        {
            Passphrase = string.Empty;
            ConfirmedPassphrase = string.Empty;
        }
    }

    private Models.SSHKeyType _type = Models.SSHKeyType.Supported[0];
    private string _name;
    private string _email;
    private bool _usePassphrase;
    private string _passphrase;
    private string _confirmedPassphrase;
    private string _errorMessage;
}
