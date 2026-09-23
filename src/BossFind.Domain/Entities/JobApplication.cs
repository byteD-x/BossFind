namespace BossFind.Domain.Entities;

public enum JobApplicationStatus
{
    Draft,
    ReadyForReview,
    Submitted,
    Withdrawn,
    Failed
}

public sealed class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobPostingId { get; set; }

    public JobPosting JobPosting { get; set; } = null!;

    public Guid? CandidateProfileId { get; set; }

    public CandidateProfile? CandidateProfile { get; set; }

    public JobApplicationStatus Status { get; set; } = JobApplicationStatus.Draft;

    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
