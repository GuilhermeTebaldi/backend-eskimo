//CSharpAssistant.API/DTOs/StoreCustomerDTOs.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace CSharpAssistant.API.DTOs
{
    public class StoreCustomerRegisterDTO
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? Nickname { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class StoreCustomerLoginDTO
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class StoreCustomerProfileDTO
    {
        public string FullName { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Neighborhood { get; set; }
        public string? Street { get; set; }
        public string? Number { get; set; }
        public string? Complement { get; set; }
        public string? AddressLabel { get; set; }
        public string? ProfileImageBase64 { get; set; }
    }

    public class StoreCustomerResponseDTO
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Neighborhood { get; set; }
        public string? Street { get; set; }
        public string? Number { get; set; }
        public string? Complement { get; set; }
        public string? AddressLabel { get; set; }
        public string? ProfileImageBase64 { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class StoreCustomerPasswordAdminDTO
    {
        [Required, MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
