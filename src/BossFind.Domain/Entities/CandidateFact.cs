using BossFind.Domain.Enums;

namespace BossFind.Domain.Entities;

public sealed class CandidateFact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProfileId { get; set; }

    public CandidateProfile Profile { get; set; } = null!;

    public CandidateFactCategory Category { get; set; }

    public string Content { get; set; } = string.Empty;

    public string SourceText { get; set; } = string.Empty;

    public decimal Confidence { get; set; }

    public bool IsConfirmed { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public long Version { get; set; }
}
