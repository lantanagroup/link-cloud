using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Hl7.Fhir.Model.Encounter;

namespace LantanaGroup.Link.Normalization.Application.Services
{
    #region command models
    public class UnknownOperationCommand : IRequest<OperationCommandResult>
    {
        public Base Resource { get; set; }

        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class PeriodDateFixerCommand
    {
        public Base Resource { get; set; }

        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class FixResourceIDCommand
    {
        public Base Resource { get; set; }

        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class CopyElementCommand
    {
        public Base Resource { get; set; }
        public CopyElementOperation Operation { get; set; }
        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class ApplyConceptMapCommand
    {
        public Base Resource { get; set; }

        public ConceptMapOperation Operation { get; set; }
        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class ConditionalTransformationCommand
    {
        public Base Resource { get; set; }

        public ConditionalTransformationOperation Operation { get; set; }
        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class CopyLocationIdentifierToTypeCommand
    {
        public Base Resource { get; set; }

        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }
    #endregion

    public interface INormalizationService
    {
        Task<OperationCommandResult> ApplyConceptMap(ApplyConceptMapCommand request, CancellationToken cancellationToken);
        Task<OperationCommandResult> CopyElement(CopyElementCommand request, CancellationToken cancellationToken);
        Task<OperationCommandResult> ConditionalTransformation(ConditionalTransformationCommand request, CancellationToken cancellationToken);
        Task<OperationCommandResult> CopyLocationIdentifierToType(CopyLocationIdentifierToTypeCommand request, CancellationToken cancellationToken);
        Task<OperationCommandResult> FixResourceId(FixResourceIDCommand request, CancellationToken cancellationToken);
        Task<OperationCommandResult> FixPeriodDates(PeriodDateFixerCommand request, CancellationToken cancellationToken);
        Task<OperationCommandResult> UnknownOperation(UnknownOperationCommand request, CancellationToken cancellationToken);
    }

    public class NormalizationService : INormalizationService
    {
        private readonly IConditionalTransformationEvaluationService _conditionalTransformationEvaluationService;
        private readonly ILogger<NormalizationService> _logger;
        public NormalizationService(ILogger<NormalizationService> logger, IConditionalTransformationEvaluationService conditionalTransformationEvaluationService)
        {
            _conditionalTransformationEvaluationService = conditionalTransformationEvaluationService;
            _logger = logger;
        }

        public async Task<OperationCommandResult> ApplyConceptMap(ApplyConceptMapCommand request, CancellationToken cancellationToken)
        {
            var resource = request.Resource;
            var propertyChanges = request.PropertyChanges;

            ConceptMap conceptMap = null;
            if (request.Operation.FhirConceptMap is ConceptMap)
            {
                conceptMap = (ConceptMap)request.Operation.FhirConceptMap;
            }
            else
            {
                conceptMap = ((JsonElement)request.Operation.FhirConceptMap).Deserialize<ConceptMap>(new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
            }


            resource = ProcessResource(resource, request.Operation.FhirContext, conceptMap, propertyChanges);
            return new OperationCommandResult
            {
                Resource = resource,
                PropertyChanges = propertyChanges,
            };
        }

        public async Task<OperationCommandResult> ConditionalTransformation(ConditionalTransformationCommand request, CancellationToken cancellationToken)
        {
            Base resource = request.Resource;

            var operationCommandResult = new OperationCommandResult();


            List<(Base? ele, Base resource)> selectedElements = new List<(Base? ele, Base resource)>();

            selectedElements.AddRange(resource.Select(request.Operation.TransformElement).ToList().Select(x => (x, resource)));

            selectedElements.ForEach(x => AssignElement(x.ele, x.resource, request));

            operationCommandResult.PropertyChanges = request.PropertyChanges;
            operationCommandResult.Resource = resource;
            return operationCommandResult;
        }

        public async Task<OperationCommandResult> CopyElement(CopyElementCommand request, CancellationToken cancellationToken)
        {
            var resource = request.Resource;
            var propertyChanges = request.PropertyChanges;

            var resourceType = GetFhirResourceType(resource.TypeName);
            var fromFhirPath = request.Operation.FromFhirPath;
            var toFhirPath = request.Operation.ToFhirPath;
            var fromElement = resource.Select(fromFhirPath).FirstOrDefault();
            var toElement = resource.Select(toFhirPath).FirstOrDefault();
            if (fromElement != null && toElement != null)
            {
                if (toElement is Code)
                {
                    AssignElement(fromElement, (Code)toElement);
                }
                else
                {
                    toElement = new FhirString(fromElement.ToString());
                }
                propertyChanges.Add(new PropertyChangeModel
                {
                    InitialPropertyValue = toElement?.ToString(),
                    NewPropertyValue = fromElement?.ToString(),
                    PropertyName = $"{resourceType}.{toFhirPath}"
                });
            }

            return new OperationCommandResult
            {
                Resource = resource,
                PropertyChanges = propertyChanges
            };
        }

        public async Task<OperationCommandResult> CopyLocationIdentifierToType(CopyLocationIdentifierToTypeCommand request, CancellationToken cancellationToken)
        {
            var resource = request.Resource;
            var propertyChanges = request.PropertyChanges;
            if (resource.TypeName == ResourceType.Location.ToString())
            {
                var locationResource = (Location)resource;
                var identifiers = locationResource.Identifier;
                var types = locationResource.Type;
                if (identifiers.Count > 0)
                {
                    foreach (var identifier in identifiers)
                    {
                        var codings = new List<Coding>();
                        var type = new CodeableConcept();
                        var coding = new Coding();
                        coding.Code = identifier.Value;
                        coding.System = identifier.System;

                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(coding.Code),
                            NewPropertyValue = coding.Code,
                        });
                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(coding.System),
                            NewPropertyValue = coding.System,
                        });
                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(codings),
                            InitialPropertyValue = codings?.ToString(),
                        });

                        codings.Add(coding);

                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(codings),
                            NewPropertyValue = codings?.ToString(),
                        });

                        type.Coding = codings;

                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = nameof(type.Coding),
                            NewPropertyValue = type.Coding.ToString(),
                        });

                        types.Add(type);
                    }
                    var locationType = ((Location)resource).Type;
                    propertyChanges.Add(new PropertyChangeModel
                    {
                        PropertyName = nameof(locationType),
                        InitialPropertyValue = locationType?.ToString(),
                        NewPropertyValue = types?.ToString()
                    });
                    ((Location)resource).Type = types;
                }

            }

            return new OperationCommandResult
            {
                Resource = resource,
                PropertyChanges = propertyChanges,
            };
        }

        public async Task<OperationCommandResult> FixResourceId(FixResourceIDCommand request, CancellationToken cancellationToken)
        {
            var resource = (Resource)request.Resource;
            var propertyChanges = request.PropertyChanges;
            string oldId = string.Empty;
            string newId = string.Empty;
            string newIdRef = string.Empty;

            List<Bundle.EntryComponent> invalidEntries = new List<Bundle.EntryComponent>();


            if (resource != null
            && resource.Id != null
            && (resource.Id.Length > 64 || resource.Id.StartsWith(NormalizationConstants.FixResourceIDCommand.UuidPrefix)))
            {
                Bundle.EntryComponent bundleEntryComponent = new Bundle.EntryComponent
                {
                    Resource = resource
                };
                invalidEntries.Add(bundleEntryComponent);
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

                FindReferencesAndUpdate(resource, oldId, newId, invalidEntry.Resource.TypeName);
            }

            return new OperationCommandResult
            {
                Resource = resource,
                PropertyChanges = propertyChanges,
            };
        }

        public async Task<OperationCommandResult> FixPeriodDates(PeriodDateFixerCommand request, CancellationToken cancellationToken)
        {
            var resource = request.Resource;
            var propertyChanges = request.PropertyChanges;
            var operationCommandResult = new OperationCommandResult();


            foreach (var ele in resource.NamedChildren.Where(x => x.ElementName.Equals("Period", System.StringComparison.OrdinalIgnoreCase)))
            {
                var period = (Period)ele.Value;
                var endDate = period.EndElement;
                var startDate = period.StartElement;

                if (endDate != null && (endDate.Value.Length != startDate.Value.Length))
                {
                    DateTime.TryParse(endDate.Value, out DateTime endDateTime);
                    DateTime.TryParse(startDate.Value, out DateTime startDateTime);

                    period.EndElement.Value = endDateTime.ToString("yyyy-MM-ddThh:mm:ss") + "Z";
                    period.StartElement.Value = startDateTime.ToString("yyyy-MM-ddThh:mm:ss") + "Z";

                    propertyChanges.Add(new PropertyChangeModel
                    {
                        PropertyName = $"{resource.TypeName}.period",
                        InitialPropertyValue = endDate.ToString(),
                        NewPropertyValue = endDateTime.ToString("yyyy-MM-ddThh:mm:ss") + "Z"
                    });
                }

            }


            operationCommandResult.PropertyChanges = request.PropertyChanges;
            operationCommandResult.Resource = resource;
            return operationCommandResult;
        }

        public async Task<OperationCommandResult> UnknownOperation(UnknownOperationCommand request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
        
        #region Private Methods
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

        private ResourceType GetFhirResourceType(string requestedResource)
        {
            var success = Enum.TryParse(typeof(ResourceType), requestedResource, out var resourceType);
            if (success)
            {
                return (ResourceType)resourceType!;
            }
            throw new Exception($"Resource Not found for name {requestedResource}");
        }

        private void AssignElement(Base? fromElement, Code toElement)
        {
            if (fromElement is Code)
            {
                toElement.Value = ((Code)fromElement).Value;
            }
            else
            {
                toElement.Value = fromElement?.ToString();
            }
        }

        private void AssignElement(Base eleToUpdate, Base resource, ConditionalTransformationCommand request)
        {
            var evaluationOutcome = _conditionalTransformationEvaluationService.Evaluate(request.Operation, (Resource)resource);

            if (evaluationOutcome.AllConditionsMet)
            {
                var oldElementValue = eleToUpdate.ToString();
                if (eleToUpdate is Code)
                {
                    ((Code)eleToUpdate).Value = request.Operation.TransformValue;
                }
                else if (eleToUpdate is Code<EncounterStatus>)
                {
                    ((Code<EncounterStatus>)eleToUpdate).Value = (EncounterStatus)Enum.Parse(typeof(EncounterStatus), request.Operation.TransformValue, true);
                }
                else
                {
                    eleToUpdate = new FhirString(request.Operation.TransformValue);
                }

                request.PropertyChanges.Add(new PropertyChangeModel
                {
                    InitialPropertyValue = oldElementValue,
                    NewPropertyValue = eleToUpdate.ToString(),
                    PropertyName = $"{request.Operation.TransformResource}.{request.Operation.TransformElement}"
                });

            }
        }

        /// <summary>
        /// 1. loop through each resource in bundle
        /// 2. get code resource by resource path list
        /// 3. apply concept map to resource
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="fhirPath"></param>
        /// <returns></returns>
        private Base ProcessResource(Base receivedResource, string fhirContext, ConceptMap conceptMap, List<PropertyChangeModel> propertyChanges)
        {
            var splitContext = fhirContext.Split('.');

            if (splitContext.Length > 0)
            {
                var resource = splitContext[0];
                splitContext = splitContext.Skip(1).ToArray();

                if (!((Resource)receivedResource).TypeName.Equals(resource, StringComparison.InvariantCultureIgnoreCase))
                {
                    return receivedResource;
                }

                if (splitContext.Length > 0)
                {
                    List<Base?> selectedElements = new List<Base?>();

                    var elements = receivedResource.Select($"{fhirContext}");
                    selectedElements.AddRange(receivedResource.Select($"{fhirContext}"));

                    selectedElements.ForEach(x => ProcessElement(x, conceptMap, propertyChanges));
                }
                else
                {
                    ProcessResource(receivedResource, conceptMap, propertyChanges);
                }
            }
            else
            {
                ProcessResource(receivedResource, conceptMap, propertyChanges);
            }

            return receivedResource;
        }

        private Base ProcessResource(Base resource, ConceptMap conceptMap, List<PropertyChangeModel> propertyChanges)
        {
            foreach (var element in resource.Children)
            {
                ProcessElement(element, conceptMap, propertyChanges);
            }
            return resource;
        }

        private void ProcessElement(Base element, ConceptMap conceptMap, List<PropertyChangeModel> propertyChanges)
        {
            if (element is Coding)
            {
                foreach (var group in conceptMap.Group)
                {
                    if (group.Source.Equals(((Coding)element).System))
                    {
                        var elements = group.Element.Where(x => x.CodeElement.Value.Equals(((Coding)element).Code)).ToList();
                        if (elements.Count > 0)
                        {
                            var rawCode = (Coding)element;
                            var originalCode = new Extension
                            {
                                Url = NormalizationConstants.ConceptMap.ExtensionName,
                                Value = new Coding
                                {
                                    Code = rawCode.Code,
                                    System = rawCode.System,
                                    Display = rawCode.Display,
                                    Version = rawCode.Version
                                }
                            };
                            if (((Coding)element).Extension == null)
                            {
                                ((Coding)element).Extension = new List<Extension>();
                            }

                            ((Coding)element).Extension.Add(originalCode);
                            ((Coding)element).System = group.Target;
                            ((Coding)element).Display = elements.FirstOrDefault()?.Target?.FirstOrDefault()?.Display;
                            ((Coding)element).Code = elements.FirstOrDefault()?.Target?.FirstOrDefault()?.Code;
                        }

                    }
                }
            }
            else if (element is CodeableConcept)
            {
                if (((CodeableConcept)element).Coding.Count > 0)
                {
                    foreach (var group in conceptMap.Group)
                    {
                        if (group.Source.Equals(((CodeableConcept)element).Coding?.FirstOrDefault()?.System))
                        {
                            var elements = group.Element.Where(x => x.CodeElement.Value.Equals(((CodeableConcept)element).Coding?.FirstOrDefault()?.Code)).ToList();
                            if (elements.Count > 0)
                            {
                                var originalCoding = ((CodeableConcept)element).Coding[0];
                                var newCoding = new Coding
                                {
                                    System = group.Target,
                                    Display = elements.FirstOrDefault()?.Target?.FirstOrDefault()?.Display,
                                    Code = elements.FirstOrDefault()?.Target?.FirstOrDefault()?.Code,
                                    Extension = new List<Extension>
                                    {
                                        new Extension
                                        {
                                            Url = NormalizationConstants.ConceptMap.ExtensionName,
                                            Value = new CodeableConcept
                                            {
                                                Coding = new List<Coding>
                                                {
                                                    new Coding
                                                    {
                                                        Code = originalCoding.Code,
                                                        System = originalCoding.System,
                                                        Display= originalCoding.Display,
                                                    }
                                                }
                                            }
                                        }
                                    }
                                };

                                ((CodeableConcept)element).Coding[0] = newCoding;
                            }

                        }
                    }
                }
            }
            else if (element is Code)
            {
                foreach (var group in conceptMap.Group)
                {
                    if (group.Source.Equals(((Code)element).ToSystemCode().System))
                    {
                        var elements = group.Element.Where(x => x.CodeElement.Value.Equals(((Code)element).ToSystemCode().Value)).ToList();
                        if (elements.Count > 0)
                        {
                            var originalCode = new Extension
                            {
                                Url = NormalizationConstants.ConceptMap.ExtensionName,
                                Value = (Code)element
                            };
                            if (((Code)element).Extension == null)
                            {
                                ((Code)element).Extension = new List<Extension>();
                            }
                            ((Code)element).Extension.Add(originalCode);

                            ((Code)element).Value = elements.FirstOrDefault()?.Target?.FirstOrDefault()?.Code;
                        }

                    }
                }
            }
        }
        private void ProcessResource(Element element, ConceptMap conceptMap, List<PropertyChangeModel> propertyChanges)
        {
            //foreach (var element in resource.Children)
            //{
            if (element is Coding)
            {
                foreach (var group in conceptMap.Group)
                {
                    if (group.Source.Equals(((Coding)element).System))
                    {
                        var elements = group.Element.Where(x => x.CodeElement.Value.Equals(((Coding)element).Code)).ToList();
                        if (elements.Count > 0)
                        {
                            var rawCode = (Coding)element;
                            var originalCode = new Extension
                            {
                                Url = NormalizationConstants.ConceptMap.ExtensionName,
                                Value = new Coding
                                {
                                    Code = rawCode.Code,
                                    System = rawCode.System,
                                    Display = rawCode.Display,
                                    Version = rawCode.Version
                                }
                            };
                            if (((Coding)element).Extension == null)
                            {
                                ((Coding)element).Extension = new List<Extension>();
                            }

                            ((Coding)element).Extension.Add(originalCode);
                            ((Coding)element).System = group.Target;
                            ((Coding)element).Display = elements.FirstOrDefault()?.Target?.FirstOrDefault()?.Display;
                            ((Coding)element).Code = elements.FirstOrDefault()?.Target?.FirstOrDefault()?.Code;
                        }

                    }
                }
            }
            else if (element is CodeableConcept)
            {
                if (((CodeableConcept)element).Coding.Count > 0)
                {
                    foreach (var group in conceptMap.Group)
                    {
                        if (group.Source.Equals(((CodeableConcept)element).Coding?.FirstOrDefault()?.System))
                        {
                            var elements = group.Element.Where(x => x.CodeElement.Value.Equals(((CodeableConcept)element).Coding?.FirstOrDefault()?.Code)).ToList();
                            if (elements.Count > 0)
                            {
                                var originalCoding = ((CodeableConcept)element).Coding[0];
                                var newCoding = new Coding
                                {
                                    System = group.Target,
                                    Display = elements[0].Display,
                                    Code = elements[0].Code,
                                    Extension = new List<Extension>
                                    {
                                        new Extension
                                        {
                                            Url = NormalizationConstants.ConceptMap.ExtensionName,
                                            Value = new CodeableConcept
                                            {
                                                Coding = new List<Coding>
                                                {
                                                    new Coding
                                                    {
                                                        Code = originalCoding.Code,
                                                        System = originalCoding.System,
                                                        Display= originalCoding.Display,
                                                    }
                                                }
                                            }
                                        }
                                    }
                                };

                                ((CodeableConcept)element).Coding[0] = newCoding;
                            }

                        }
                    }
                }
            }
            else if (element is Code)
            {
                foreach (var group in conceptMap.Group)
                {
                    if (group.Source.Equals(((Code)element).ToSystemCode().System))
                    {
                        var elements = group.Element.Where(x => x.CodeElement.Value.Equals(((Code)element).ToSystemCode().Value)).ToList();
                        if (elements.Count > 0)
                        {
                            var originalCode = new Extension
                            {
                                Url = NormalizationConstants.ConceptMap.ExtensionName,
                                Value = (Code)element
                            };
                            if (((Code)element).Extension == null)
                            {
                                ((Code)element).Extension = new List<Extension>();
                            }
                            ((Code)element).Extension.Add(originalCode);

                            ((Code)element).Value = elements.FirstOrDefault()?.Target?.FirstOrDefault()?.Code;
                        }

                    }
                }
            }
        }
        #endregion
    }
}
