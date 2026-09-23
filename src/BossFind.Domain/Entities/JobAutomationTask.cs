namespace BossFind.Domain.Entities;

public enum JobAutomationTaskStatus
{
    Queued,
    InReview,
    Completed,
    Cancelled,
    Failed
}

public sealed class JobAutomationTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobPostingId { get; set; }

    public JobPosting JobPosting { get; set; } = null!;

    public Guid? CandidateProfileId { get; set; }

    public JobAutomationTaskStatus Status { get; set; } = JobAutomationTaskStatus.Queued;

    public string ErrorMessage { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }
}
