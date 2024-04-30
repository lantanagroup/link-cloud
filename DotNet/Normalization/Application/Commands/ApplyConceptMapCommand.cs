using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Commands;

public class ApplyConceptMapCommand : IRequest<OperationCommandResult>
{
    public Base Resource { get; set; }

    public ConceptMapOperation Operation { get; set; }
    public List<PropertyChangeModel> PropertyChanges { get; set; }
}

public class ApplyConceptMapHandler : IRequestHandler<ApplyConceptMapCommand, OperationCommandResult>
{
    private readonly ILogger<ApplyConceptMapHandler> _logger;

    public ApplyConceptMapHandler(ILogger<ApplyConceptMapHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OperationCommandResult> Handle(ApplyConceptMapCommand request, CancellationToken cancellationToken)
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

        if(splitContext.Length > 0)
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

    private Base ProcessResource(
        Base resource,
        ConceptMap conceptMap,
        List<PropertyChangeModel> propertyChanges)
    {
        foreach (var element in resource.Children)
        {
            ProcessElement(element, conceptMap, propertyChanges);
        }
        return resource;
    }

    private void ProcessElement(
        Base element,
        ConceptMap conceptMap,
        List<PropertyChangeModel> propertyChanges)
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
    private void ProcessResource(
        //Resource resource, 
        Element element,
        ConceptMap conceptMap, 
        List<PropertyChangeModel> propertyChanges)
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
}
