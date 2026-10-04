using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Access;

public sealed partial class AccessService
{
    public async Task<(string? Token, string Message)> Login(string? email, string? password)
    {
        const string generic = "Credenciales incorrectas.";
        if (!ValidEmail(email) || string.IsNullOrEmpty(password)) return (null, generic);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email!.Trim().ToLowerInvariant());
        if (user is null) return (null, generic);
        if (!user.EmailConfirmed) return (null, "La cuenta no está activa. Revisa el correo de activación.");
        if (!user.IsEnabled) return (null, "La cuenta está desactivada.");
        if (user.LockedUntilUtc > Now) return (null, "Cuenta bloqueada temporalmente. Intenta más tarde.");
        var verified = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verified == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5) user.LockedUntilUtc = Now.AddMinutes(15);
            await db.SaveChangesAsync();
            return (null, generic);
        }
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, password);
        var secret = NewSecret();
        db.Sessions.Add(new UserSession { UserId = user.Id, TokenHash = HashSecret(secret),
            CreatedUtc = Now, ExpiresUtc = Now.AddHours(12) });
        await db.SaveChangesAsync();
        return (secret, "Sesión iniciada.");
    }

    public async Task Logout(UserSession session)
    {
        session.RevokedUtc = Now;
        await db.SaveChangesAsync();
    }

}
