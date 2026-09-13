using PetChat.ApiServer.Utils;

namespace PetChat.ApiServer.Tests.Unit;

/// <summary>
/// <see cref="PasswordValidator"/> - тесты требований к паролю при регистрации:
/// - минимум одна заглавная буква
/// - минимум одна цифра
/// - минимум один спецсимвол из набора @$!%*?&
/// Тесты фиксируют границы регекса.
/// </summary>
public class PasswordValidatorTests
{
    [Theory]
    [InlineData("Aa1!aaaa")] // минимально допустимый пароль
    [InlineData("SuperP@ssword1!")]
    public void IsSafe_ValidPasswords_ReturnTrue(string password)
    {
        Assert.True(PasswordValidator.IsSafe(password));
    }

    [Fact]
    public void IsSafe_ContainsCyrillicCharacters_ReturnsFalse()
    {
        // проверка на кириллицу
        Assert.False(PasswordValidator.IsSafe("Пароль1!Aa"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSafe_NullOrWhitespace_ReturnsFalse(string? password)
    {
        Assert.False(PasswordValidator.IsSafe(password!));
    }

    [Theory]
    [InlineData("Aa1!aaa")] // 7 символов — на 1 меньше минимума
    public void IsSafe_TooShort_ReturnsFalse(string password)
    {
        Assert.False(PasswordValidator.IsSafe(password));
    }

    [Theory]
    [InlineData("aaaaaaa1!")] // нет заглавной буквы
    [InlineData("AAAAAAA1!")] // нет строчной буквы
    [InlineData("Aaaaaaaa!")] // нет цифры
    [InlineData("Aaaaaaaa1")] // нет спецсимвола
    public void IsSafe_MissingRequiredCharacterClass_ReturnsFalse(string password)
    {
        Assert.False(PasswordValidator.IsSafe(password));
    }

    [Fact]
    public void IsSafe_DisallowedSpecialCharacter_ReturnsFalse()
    {
        // проверка на неразрешенные спецсимволы
        Assert.False(PasswordValidator.IsSafe("Aaaaaaa1#"));
    }
}