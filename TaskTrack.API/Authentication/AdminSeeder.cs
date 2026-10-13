using System.ComponentModel.DataAnnotations;
using TaskTrack.Repo.Interfaces;
using TaskTrack.Repo.Models;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Services;

namespace TaskTrack.API.Authentication;

public static class AdminSeeder
{
    public static async Task RunAsync(IServiceProvider services)
    {
        var name = Environment.GetEnvironmentVariable("SEED_ADMIN_NAME");
        var email = Environment.GetEnvironmentVariable("SEED_ADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");
        if (name is null) { Console.Write("Admin full name: "); name = Console.ReadLine() ?? ""; }
        if (email is null) { Console.Write("Admin email: "); email = Console.ReadLine() ?? ""; }
        if (password is null)
        {
            Console.Write("Admin password (hidden): "); password = "";
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password = password[..^1]; }
                else if (!char.IsControl(key.KeyChar)) password += key.KeyChar;
            }
            Console.WriteLine();
        }
        var request = new RegisterRequest { FullName = name, Email = email, Password = password };
        Validator.ValidateObject(request, new ValidationContext(request), true);
        using var scope = services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        await accounts.TransactionAsync(async () =>
        {
            var normalized = email.Trim().ToLowerInvariant();
            if (await accounts.FindEmailAsync(normalized, default) is not null) throw new InvalidOperationException("Email already exists; seed never changes existing accounts.");
            accounts.Add(new SystemAccount { FullName = name.Trim(), Email = normalized, PasswordHash = PasswordHasher.Hash(password), Role = 1 });
            await accounts.SaveAsync(default);
            return true;
        }, default);
        Console.WriteLine("Admin inserted directly into the database. Record credentials privately for submission.");
    }
}
