using System.Text.RegularExpressions;

namespace PetChat.ApiServer.Utils;

public static partial class PasswordValidator
{
    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$")]
    private static partial Regex GetPasswordRegex();

    public static bool IsSafe(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return false;
        return GetPasswordRegex().IsMatch(password);
    }
}