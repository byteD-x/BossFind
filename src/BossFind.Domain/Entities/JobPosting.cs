namespace BossFind.Domain.Entities;

public sealed class JobPosting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Platform { get; set; } = "Boss直聘";

    public string ExternalId { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Company { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Salary { get; set; } = string.Empty;

    public string Experience { get; set; } = string.Empty;

    public string Education { get; set; } = string.Empty;

    public string Benefits { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Skills { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastViewedAtUtc { get; set; } = DateTime.UtcNow;

    public int ViewCount { get; set; }
}
