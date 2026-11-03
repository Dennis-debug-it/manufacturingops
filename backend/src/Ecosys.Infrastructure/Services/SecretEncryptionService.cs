using Microsoft.AspNetCore.DataProtection;

namespace Ecosys.Infrastructure.Services;

public interface ISecretEncryptionService
{
    string? Encrypt(string? plaintext);
    string? Decrypt(string? ciphertext);
}

internal sealed class SecretEncryptionService(IDataProtectionProvider dataProtectionProvider) : ISecretEncryptionService
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Ecosys.ServiceOps.SettingsSecrets.v1");

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        return protector.Protect(plaintext.Trim());
    }

    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            return null;
        }

        try
        {
            return protector.Unprotect(ciphertext);
        }
        catch
        {
            return null;
        }
    }
}
