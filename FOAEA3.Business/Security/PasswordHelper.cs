using System.Security.Cryptography;
using System.Text;

namespace FOAEA3.Business.Security;

public class PasswordHelper
{
    public const string ConfirmationCode = "ConfirmationCode";

    public static bool IsValidPassword(string enteredPassword, string salt, string existingEncryptedPassword)
    {
        string enteredPasswordEncrypted = EncodePassword(enteredPassword, salt);

        return enteredPasswordEncrypted.Equals(existingEncryptedPassword, StringComparison.OrdinalIgnoreCase);
    }

    private static string EncodePassword(string password, string salt)
    {
        var passwordBytes = Encoding.Unicode.GetBytes(password);
        var saltBase64 = Convert.FromBase64String(salt);
        var saltAndPasswordBytes = new byte[saltBase64.Length + (passwordBytes.Length - 1) + 1];
        byte[] encodedPassword = null;
        Buffer.BlockCopy(saltBase64, 0, saltAndPasswordBytes, 0, saltBase64.Length);
        Buffer.BlockCopy(passwordBytes, 0, saltAndPasswordBytes, saltBase64.Length, passwordBytes.Length);

        using (var sha256encryption = SHA256.Create())
        {
            encodedPassword = sha256encryption.ComputeHash(saltAndPasswordBytes);
        }

        return Convert.ToBase64String(encodedPassword);
    }

}
