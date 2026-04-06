namespace CHRONIQ.Services;

/// <summary>
/// Encrypts and decrypts notification secrets before persistence.
/// </summary>
public interface INotificationSecretProtector
{
    /// <summary>
    /// Encrypts plain text secret.
    /// </summary>
    string Protect(string plainText);

    /// <summary>
    /// Decrypts encrypted secret.
    /// </summary>
    string Unprotect(string protectedText);
}
