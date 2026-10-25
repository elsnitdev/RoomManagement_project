using System.ComponentModel.DataAnnotations;
using RoomManagement.Business.Exceptions;

namespace RoomManagement.Business.Validators;

public static class UserAccountRules
{
    public const int MaximumEmailLength = 256;

    public static void EnsureValidEmail(string? email)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumEmailLength ||
            trimmed.ToUpperInvariant().Length > MaximumEmailLength || !new EmailAddressAttribute().IsValid(trimmed))
        {
            throw new AppValidationException("Email phải đúng định dạng và không vượt quá 256 ký tự.");
        }
    }
}
