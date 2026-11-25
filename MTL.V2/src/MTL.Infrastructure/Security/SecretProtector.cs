using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace MTL.Infrastructure.Security;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

public sealed class SecretProtectionException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);

public sealed class SecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector =
        provider.CreateProtector("MTL.V2.Integrations.Secrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext)
    {
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (CryptographicException exception)
        {
            throw new SecretProtectionException(
                "Không thể giải mã thông tin bảo mật đã lưu vì bộ khóa Data Protection không khớp với database hiện tại.",
                exception);
        }
    }
}

