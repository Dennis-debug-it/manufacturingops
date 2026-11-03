namespace Ecosys.Shared.Options;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
