using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LantanaGroup.Automation.Generation;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Text;
using System.Text.Json;

namespace Automation.UI.Services.Persistence;

[BsonIgnoreExtraElements]
public sealed class GeneratedPatientTemplateDocument
{
    [BsonId]
    public string Key { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public long ByteCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MongoGeneratedPatientTemplateCache : IGeneratedPatientTemplateCache
{
    private readonly IMongoCollection<GeneratedPatientTemplateDocument> _templates;
    private readonly BlobContainerClient _container;
    private readonly string _blobRoot;
    private readonly ILogger<MongoGeneratedPatientTemplateCache> _logger;

    public MongoGeneratedPatientTemplateCache(
        IMongoDatabase database,
        IOptions<ImportedBundleBlobStorageSettings> settings,
        ILogger<MongoGeneratedPatientTemplateCache> logger)
    {
        _templates = database.GetCollection<GeneratedPatientTemplateDocument>("automation_generated_patient_templates");
        _logger = logger;

        var blobSettings = settings.Value;
        if (string.IsNullOrWhiteSpace(blobSettings.ConnectionString))
            throw new InvalidOperationException("InternalBlobStorage:ConnectionString is required for generated patient templates.");
        if (string.IsNullOrWhiteSpace(blobSettings.BlobContainerName))
            throw new InvalidOperationException("InternalBlobStorage:BlobContainerName is required for generated patient templates.");

        _container = new BlobContainerClient(blobSettings.ConnectionString, blobSettings.BlobContainerName);
        var configuredRoot = string.IsNullOrWhiteSpace(blobSettings.GeneratedTemplateBlobRoot)
            ? string.Empty
            : blobSettings.GeneratedTemplateBlobRoot.Trim('/');
        _blobRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? "generated-patient-templates"
            : $"{configuredRoot}/generated-patient-templates";
    }

    public async Task<GeneratedPatientTemplate?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var doc = await _templates.Find(template => template.Key == key).FirstOrDefaultAsync(ct);
            if (doc == null || string.IsNullOrWhiteSpace(doc.BlobName))
                return null;

            var blob = _container.GetBlobClient(doc.BlobName);
            var exists = await blob.ExistsAsync(ct);
            if (!exists.Value)
                return null;

            var download = await blob.DownloadContentAsync(ct);
            var payload = JsonSerializer.Deserialize<TemplatePayload>(download.Value.Content.ToString());
            if (payload == null || string.IsNullOrWhiteSpace(payload.TemplateRunTag) || payload.BundleJson is not { Count: > 0 })
                return null;

            return new GeneratedPatientTemplate(payload.TemplateRunTag, payload.BundleJson);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read generated patient template cache entry for key '{TemplateKey}'.", key);
            return null;
        }
    }

    public async Task StoreAsync(string key, GeneratedPatientTemplate template, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Template key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(template.TemplateRunTag))
            throw new InvalidOperationException("Template run tag is required.");
        if (template.BundleJson.Count == 0)
            throw new InvalidOperationException("Template bundles are required.");

        try
        {
            var payload = new TemplatePayload(template.TemplateRunTag, template.BundleJson.ToList());
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            var contentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

            await _container.CreateIfNotExistsAsync(cancellationToken: ct);

            var blobName = $"{_blobRoot}/{key}-{contentHash}.json";
            var blob = _container.GetBlobClient(blobName);
            if (!(await blob.ExistsAsync(ct)).Value)
            {
                using var stream = new MemoryStream(bytes, writable: false);
                await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
                await blob.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = "application/json" }, cancellationToken: ct);
            }

            var now = DateTimeOffset.UtcNow;
            var update = Builders<GeneratedPatientTemplateDocument>.Update
                .Set(d => d.ContentHash, contentHash)
                .Set(d => d.BlobName, blobName)
                .Set(d => d.ByteCount, bytes.LongLength)
                .Set(d => d.UpdatedAt, now)
                .SetOnInsert(d => d.Key, key)
                .SetOnInsert(d => d.CreatedAt, now);

            await _templates.UpdateOneAsync(d => d.Key == key, update, new UpdateOptions { IsUpsert = true }, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store generated patient template cache entry for key '{TemplateKey}'.", key);
            return;
        }
    }

    private sealed record TemplatePayload(string TemplateRunTag, List<string> BundleJson);
}
