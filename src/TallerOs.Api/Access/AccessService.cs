using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Access;

public sealed partial class AccessService(AppDbContext db, IPasswordHasher<UserAccount> hasher, TimeProvider clock,
    IConfiguration configuration)
{
    public static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string HashSecret(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public DateTimeOffset Now => clock.GetUtcNow();

    public static bool ValidEmail(string? email) => !string.IsNullOrWhiteSpace(email)
        && new EmailAddressAttribute().IsValid(email) && email.Length <= 254;

    public static bool ValidPassword(string? password) => password is { Length: >= 8 }
        && password.Any(char.IsLetter) && password.Any(char.IsDigit);

    public async Task<(bool Ok, string Message)> Register(string? name, string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || !ValidEmail(email))
            return (false, "Nombre o correo no válido.");
        if (!ValidPassword(password))
            return (false, "La contraseña debe tener al menos 8 caracteres, letras y números.");
        var normalized = email!.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == normalized)) return (false, "El correo ya está registrado.");
        var user = new UserAccount { Name = name.Trim(), Email = normalized };
        user.PasswordHash = hasher.HashPassword(user, password!);
        db.Users.Add(user);
        QueueActivation(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return (false, "El correo ya está registrado."); }
        return (true, "Cuenta creada. Revisa tu correo para activarla.");
    }

    private void QueueActivation(UserAccount user)
    {
        var secret = NewSecret();
        db.ActivationTokens.Add(new ActivationToken { UserId = user.Id, TokenHash = HashSecret(secret),
            ExpiresUtc = Now.AddHours(24) });
        var baseUrl = configuration["APP_BASE_URL"]?.TrimEnd('/') ?? "http://localhost:5000";
        QueueEmail(user.Email, "Activa tu cuenta de TallerOS", $"Abre este enlace para activar tu cuenta: {baseUrl}/api/access/activate?token={secret}");
    }

    public async Task<bool> Activate(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return false;
        var token = await db.ActivationTokens.FirstOrDefaultAsync(x => x.TokenHash == HashSecret(secret));
        if (token is null || token.UsedUtc is not null || token.ExpiresUtc <= Now) return false;
        var user = await db.Users.FindAsync(token.UserId);
        if (user is null || user.EmailConfirmed) return false;
        token.UsedUtc = Now;
        user.EmailConfirmed = true;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task ResendActivation(string? email)
    {
        if (!ValidEmail(email)) return;
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email!.Trim().ToLowerInvariant());
        if (user is null || user.EmailConfirmed || !user.IsEnabled) return;
        var old = await db.ActivationTokens.Where(x => x.UserId == user.Id && x.UsedUtc == null).ToListAsync();
        foreach (var item in old) item.UsedUtc = Now;
        QueueActivation(user);
        await db.SaveChangesAsync();
    }

    public void QueueEmail(string recipient, string subject, string body) => db.EmailOutbox.Add(new QueuedEmail
    {
        Recipient = recipient, Subject = subject, Body = body, CreatedUtc = Now
    });
}
