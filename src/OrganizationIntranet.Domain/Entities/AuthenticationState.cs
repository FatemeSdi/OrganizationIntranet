namespace OrganizationIntranet.Domain.Entities;

public sealed class AuthenticationSettings
{
    public int Id { get; set; } = 1;
    public string Json { get; set; } = "{}";
    public string? ProtectedSmsApiKey { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

public sealed class AuthenticationChallenge
{
    public string Id { get; set; } = "";
    public long UserId { get; set; }
    public string Purpose { get; set; } = "";
    public string LoginMethod { get; set; } = "password";
    public string SecurityStamp { get; set; } = "";
    public Guid SettingsRevision { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastSentAt { get; set; }
    public string? CodeHash { get; set; }
    public string? ProtectedSecret { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
