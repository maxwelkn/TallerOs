using Microsoft.Extensions.Configuration;
using TallerOs.Api.Mail;

namespace TallerOs.IntegrationTests;

public sealed class MailConfigurationTests
{
    [Fact]
    public void NormalSmtp_PreservesTlsAndAuthenticationDefaults()
    {
        var settings = new SmtpSettings(Configuration());

        Assert.True(settings.UseTls);
        Assert.True(settings.UseAuthentication);
        Assert.Equal(587, settings.Port);
    }

    [Theory]
    [InlineData("SMTP_USE_TLS")]
    [InlineData("SMTP_USE_AUTHENTICATION")]
    public void InsecureSmtp_RequiresExplicitDemoMode(string setting)
    {
        var configuration = Configuration();
        configuration[setting] = "false";

        Assert.Throws<InvalidOperationException>(() => new SmtpSettings(configuration));
    }

    [Fact]
    public void LocalDemoSmtp_DoesNotRequireProviderCredentials()
    {
        var configuration = Configuration();
        configuration["DEMO_MODE"] = "true";
        configuration["SMTP_USE_TLS"] = "false";
        configuration["SMTP_USE_AUTHENTICATION"] = "false";
        configuration["SMTP_USER"] = "";
        configuration["SMTP_PASSWORD"] = "";

        var settings = new SmtpSettings(configuration);

        Assert.False(settings.UseTls);
        Assert.False(settings.UseAuthentication);
    }

    [Theory]
    [InlineData("SMTP_HOST", "")]
    [InlineData("SMTP_FROM", "")]
    [InlineData("SMTP_USER", "")]
    [InlineData("SMTP_PASSWORD", "")]
    [InlineData("SMTP_PORT", "0")]
    [InlineData("SMTP_PORT", "65536")]
    [InlineData("SMTP_PORT", "invalid")]
    public void InvalidSmtpConfiguration_IsRejected(string setting, string value)
    {
        var configuration = Configuration();
        configuration[setting] = value;

        Assert.Throws<InvalidOperationException>(() => new SmtpSettings(configuration));
    }

    private static IConfigurationRoot Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SMTP_HOST"] = "smtp.example.test", ["SMTP_FROM"] = "sender@example.test",
            ["SMTP_USER"] = "test-user", ["SMTP_PASSWORD"] = "fictitious-test-value"
        }).Build();
}
