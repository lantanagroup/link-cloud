namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;

public interface ILocalizationResourceService
{
    Task<LocalizationResourceResult> GetNamespaceAsync(string locale, string namespaceName, CancellationToken cancellationToken);
}

public sealed record LocalizationResourceResult(
    LocalizationResourceStatus Status,
    string? JsonPayload = null,
    string? ETag = null,
    DateTimeOffset? LastModified = null,
    string? Message = null
);

public enum LocalizationResourceStatus
{
    Ok,
    InvalidLocale,
    InvalidNamespace,
    NotFound,
    MalformedJson,
    DirectoryUnavailable
}