using MTL.Domain.Common;

namespace MTL.Domain.Integrations;

public sealed class WooCommerceConnection : CompanyOwnedEntity
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ConsumerKeyCiphertext { get; set; } = string.Empty;
    public string ConsumerSecretCiphertext { get; set; } = string.Empty;
    public string WebhookSecretCiphertext { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTimeOffset? LastSuccessfulSyncAtUtc { get; set; }
    public string? LastError { get; set; }
}

public enum SyncJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public sealed class SyncJob : CompanyOwnedEntity
{
    public string Type { get; set; } = string.Empty;
    public SyncJobStatus Status { get; set; } = SyncJobStatus.Queued;
    public string? ExternalId { get; set; }
    public int AttemptCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? Error { get; set; }
    public string? DetailsJson { get; set; }
}

