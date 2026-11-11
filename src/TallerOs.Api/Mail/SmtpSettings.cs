namespace TallerOs.Api.Mail;

public sealed class SmtpSettings
{
    public string Host { get; }
    public int Port { get; }
    public string From { get; }
    public string? Username { get; }
    public string? Password { get; }
    public bool UseTls { get; }
    public bool UseAuthentication { get; }

    public SmtpSettings(IConfiguration configuration)
    {
        Host = configuration["SMTP_HOST"] ?? "";
        From = configuration["SMTP_FROM"] ?? "";
        Username = configuration["SMTP_USER"];
        Password = configuration["SMTP_PASSWORD"];
        Port = int.TryParse(configuration["SMTP_PORT"], out var port) ? port : 587;
        UseTls = configuration.GetValue("SMTP_USE_TLS", true);
        UseAuthentication = configuration.GetValue("SMTP_USE_AUTHENTICATION", true);
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(From)
            || Port is < 1 or > 65535 || (UseAuthentication
                && (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))))
            throw new InvalidOperationException("Configura los datos SMTP requeridos para el envío.");
        if ((!UseTls || !UseAuthentication) && !configuration.GetValue("DEMO_MODE", false))
            throw new InvalidOperationException("SMTP sin TLS o autenticación requiere DEMO_MODE=true.");
    }
}
