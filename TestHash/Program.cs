using System;

class Program
{
    static void Main(string[] args)
    {
        string password = "admin123";
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        Console.WriteLine($"🔐 Hash gerado: {hashedPassword}");
    }
}
