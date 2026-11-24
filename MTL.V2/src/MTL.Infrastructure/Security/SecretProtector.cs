using Microsoft.AspNetCore.DataProtection;

namespace MTL.Infrastructure.Security;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

public sealed class SecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector =
        provider.CreateProtector("MTL.V2.Integrations.Secrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}

