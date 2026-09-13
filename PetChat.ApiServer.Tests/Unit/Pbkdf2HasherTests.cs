using PetChat.ApiServer.Utils;

namespace PetChat.ApiServer.Tests.Unit;

/// <summary>
/// <see cref="Pbkdf2Hasher"/> — хэширование и проверка паролей 
/// Тесты фиксируют хранимый формат "base64(соль):base64(хэш)"
/// </summary>
public class Pbkdf2HasherTests
{
    [Fact]
    public void HashPassword_ThenVerify_WithCorrectPassword_Succeeds()
    {
        var hash = Pbkdf2Hasher.HashPassword("Correct-P@ssw0rd-1!");

        Assert.True(Pbkdf2Hasher.VerifyPassword("Correct-P@ssw0rd-1!", hash));
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_Fails()
    {
        var hash = Pbkdf2Hasher.HashPassword("Correct-P@ssw0rd-1!");

        Assert.False(Pbkdf2Hasher.VerifyPassword("Wrong-P@ssw0rd-1!", hash));
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_ProducesDifferentHashes()
    {
        // Соль генерируется случайно на каждый вызов — одинаковый пароль
        // не должен давать одинаковую строку хэша
        var hash1 = Pbkdf2Hasher.HashPassword("SamePassword1!");
        var hash2 = Pbkdf2Hasher.HashPassword("SamePassword1!");

        Assert.NotEqual(hash1, hash2);
        Assert.True(Pbkdf2Hasher.VerifyPassword("SamePassword1!", hash1));
        Assert.True(Pbkdf2Hasher.VerifyPassword("SamePassword1!", hash2));
    }

    [Fact]
    public void HashPassword_ProducesExpectedStoredFormat()
    {
        // валидация формата
        var hash = Pbkdf2Hasher.HashPassword("AnyPassword1!");
        var parts = hash.Split(':');

        Assert.Equal(2, parts.Length);
        Assert.True(TryFromBase64(parts[0], out var salt));
        Assert.True(TryFromBase64(parts[1], out var digest));
        Assert.Equal(16, salt!.Length); // SaltSize = 16 байт (128 бит)
        Assert.Equal(32, digest!.Length); // HashSize = 32 байта (256 бит)
    }

    [Theory]
    [InlineData("")]
    [InlineData("нет двоеточия")]
    [InlineData("слишком:много:двоеточий")]
    public void VerifyPassword_IncorrectStoredHash_ReturnsFalse_DoesNotThrow(string incorrect)
    {
        // Защита от порчи данных в колонке PasswordHash — VerifyPassword должен
        // вернуть false, а не бросить исключение
        var result = Pbkdf2Hasher.VerifyPassword("любой пароль", incorrect);

        Assert.False(result);
    }

    private static bool TryFromBase64(string value, out byte[]? bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = null;
            return false;
        }
    }
}