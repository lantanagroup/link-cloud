using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace LantanaGroup.Link.Normalization.Application.Commands;

public class FixResourceIDCommand : IRequest<OperationCommandResult>
{
    public Bundle Bundle { get; set; }
    public List<PropertyChangeModel> PropertyChanges { get; set; }
}

public class FixResourceIDHandler : IRequestHandler<FixResourceIDCommand, OperationCommandResult>
{
    private readonly ILogger<FixResourceIDHandler> _logger;

    public FixResourceIDHandler(ILogger<FixResourceIDHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OperationCommandResult> Handle(FixResourceIDCommand request, CancellationToken cancellationToken)
    {
        var bundle = request.Bundle;
        var propertyChanges = request.PropertyChanges;
        string oldId = string.Empty;
        string newId = string.Empty;
        string newIdRef = string.Empty;

        List<Bundle.EntryComponent> invalidEntries = new List<Bundle.EntryComponent>();
        foreach (var entry in bundle.Entry)
        {
            if (entry.Resource != null
            && entry.Resource.Id != null
            && (entry.Resource.Id.Length > 64 || entry.Resource.Id.StartsWith(NormalizationConstants.FixResourceIDCommand.UuidPrefix)))
            {
                invalidEntries.Add(entry);
            }
        }

        // Create a dictionary where key = old id, value = new id (a hash of the old id)
        Dictionary<string, (string idOnly, string rTypePlusId)?> newIds = new Dictionary<string, (string, string)?>();
        foreach (var entry in invalidEntries)
        {
            oldId = entry.Resource.Id;
            newId = GenerateNewId(entry.Resource.Id);
            newIdRef = $"{entry.Resource.TypeName}/{newId}";
            newIds.Add(oldId, (newId, newIdRef));
        }

        _logger.LogInformation($"Found {newIds.Count} invalid entries");

        foreach (var invalidEntry in invalidEntries)
        {
            var oldEntryId = invalidEntry.Resource.Id;
            var newIdEntry = newIds[invalidEntry.Resource.Id];

            if (newIdEntry == null)
                continue;

            invalidEntry.Resource.Id = newIdEntry.Value.idOnly;

            if (invalidEntry.Resource.Meta == null)
                invalidEntry.Resource.Meta = new Meta();

            invalidEntry.Resource.Meta.Extension.Add(
                new Extension
                {
                    Url = NormalizationConstants.FixResourceIDCommand.ORIG_ID_EXT_URL,
                    Value = new FhirString(oldEntryId)
                });

            if (invalidEntry.Request != null
                && !string.IsNullOrWhiteSpace(invalidEntry.Request.Url)
                && invalidEntry.Request.Url.Equals(newIdEntry.Value.rTypePlusId, StringComparison.OrdinalIgnoreCase))
            {
                invalidEntry.Request.Url = newIdEntry.Value.rTypePlusId;
            }

            FindReferencesAndUpdate(bundle, oldId, newId, invalidEntry.Resource.TypeName);
        }

        return new OperationCommandResult
        {
            Bundle = bundle,
            PropertyChanges = propertyChanges,
        };
    }

    private void FindReferencesAndUpdate(object resource, string oldId, string newId, string resourceType)
    {
        if (resource is ResourceReference)
        {
            var origRef = (resource as ResourceReference).Reference;

            if (origRef != null)
            {
                var splitRef = (resource as ResourceReference).Reference.Split('/');
                //update ref
                if ((bool)(splitRef.LastOrDefault()?.Equals($"{resourceType}/{oldId}")))
                {
                    ((ResourceReference)resource).Reference = $"{string.Join('/', splitRef.SkipLast(1)) ?? ""}/{newId}".Trim();
                    if (((ResourceReference)resource).Extension == null)
                    {
                        ((ResourceReference)resource).Extension = new List<Extension>();
                    }
                    ((ResourceReference)resource).Extension.Add(new Extension
                    {
                        Url = NormalizationConstants.FixResourceIDCommand.ORIG_ID_EXT_URL,
                        Value = new FhirString(origRef)
                    });
                }
            }
        }

        if (resource is Bundle)
        {
            ((Bundle)resource).Entry.ForEach(entry => FindReferencesAndUpdate(entry.Resource, oldId, newId, resourceType));
        }

        if (resource is DomainResource)
        {
            ((DomainResource)resource).Children.ToList().ForEach(child => FindReferencesAndUpdate(child, oldId, newId, resourceType));
        }
    }

    private string GenerateNewId(string oldId)
    {
        var newId = oldId.Replace(NormalizationConstants.FixResourceIDCommand.UuidPrefix, "");
        if (newId.Length > 64)
        {
            var idData = Encoding.ASCII.GetBytes(newId);
            var hashData = new SHA1Managed().ComputeHash(idData);
            var hash = string.Empty;
            foreach (var b in hashData) hash += b.ToString("X2");
            newId = $"hash-{hash}";
        }
        return newId;
    }
}
