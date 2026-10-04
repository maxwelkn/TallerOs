using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Access;

public sealed partial class AccessService
{
    public async Task RequestRecovery(string? email)
    {
        if (!ValidEmail(email)) return;
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email!.Trim().ToLowerInvariant());
        if (user is null || !user.EmailConfirmed || !user.IsEnabled) return;
        await CreateRecovery(user);
        await db.SaveChangesAsync();
    }

    private async Task CreateRecovery(UserAccount user)
    {
        var old = await db.RecoveryCodes.Where(x => x.UserId == user.Id && x.UsedUtc == null).ToListAsync();
        foreach (var item in old) item.UsedUtc = Now;
        var secret = NewSecret();
        db.RecoveryCodes.Add(new RecoveryCode { UserId = user.Id, CodeHash = HashSecret(secret),
            IssuedUtc = Now, ExpiresUtc = Now.AddMinutes(30) });
        QueueEmail(user.Email, "Restablece tu contraseña de TallerOS",
            $"Tu código de un solo uso es: {secret}. Vence en 30 minutos.");
    }

    public async Task<(bool Ok, string Message)> ResetPassword(string? code, string? password)
    {
        if (!ValidPassword(password)) return (false, "La contraseña debe tener al menos 8 caracteres, letras y números.");
        if (string.IsNullOrWhiteSpace(code)) return (false, "Código no válido o vencido.");
        var item = await db.RecoveryCodes.FirstOrDefaultAsync(x => x.CodeHash == HashSecret(code));
        if (item is null || item.UsedUtc is not null || item.ExpiresUtc <= Now)
            return (false, "Código no válido o vencido.");
        var user = await db.Users.FindAsync(item.UserId);
        if (user is null || !user.IsEnabled) return (false, "Código no válido o vencido.");
        item.UsedUtc = Now;
        user.PasswordHash = hasher.HashPassword(user, password!);
        await RevokeSessions(user.Id);
        await db.SaveChangesAsync();
        return (true, "Contraseña restablecida.");
    }

    public async Task<(bool Ok, string Message)> ChangePassword(UserAccount user, string? current, string? next)
    {
        if (string.IsNullOrEmpty(current) || hasher.VerifyHashedPassword(user, user.PasswordHash, current) == PasswordVerificationResult.Failed)
            return (false, "La contraseña actual es incorrecta.");
        if (!ValidPassword(next)) return (false, "La contraseña debe tener al menos 8 caracteres, letras y números.");
        user.PasswordHash = hasher.HashPassword(user, next!);
        await RevokeSessions(user.Id);
        await db.SaveChangesAsync();
        return (true, "Contraseña actualizada. Inicia sesión nuevamente.");
    }

    public async Task ForceReset(UserAccount user)
    {
        user.PasswordHash = hasher.HashPassword(user, NewSecret());
        await RevokeSessions(user.Id);
        await CreateRecovery(user);
        await db.SaveChangesAsync();
    }

    public async Task RevokeSessions(Guid userId)
    {
        var sessions = await db.Sessions.Where(x => x.UserId == userId && x.RevokedUtc == null).ToListAsync();
        foreach (var session in sessions) session.RevokedUtc = Now;
    }

}
