namespace BossFind.Domain.Entities;

public sealed class CandidateProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ICollection<CandidateFact> Facts { get; set; } = [];

    public string Name { get; set; } = string.Empty;

    public string Headline { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public long Version { get; set; }
}
