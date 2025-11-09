using System;
using System.Collections.Generic;

namespace CSharpAssistant.API.Models
{
    public class StoreCustomer
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }
        public string? Neighborhood { get; set; }
        public string? Street { get; set; }
        public string? Number { get; set; }
        public string? Complement { get; set; }
        public string? AddressLabel { get; set; }
        public string? ProfileImageBase64 { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<Order> Orders { get; set; } = new();
    }
}
