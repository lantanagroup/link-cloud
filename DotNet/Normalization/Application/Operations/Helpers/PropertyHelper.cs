using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using System.Reflection;
using System.Text;

namespace LantanaGroup.Link.Normalization.Application.Operations.Helpers
{
    public static class PropertyHelper
    {
        public static Base GetProperty(DomainResource resource, string targetFhirPath)
        {
            var found_property = resource.Select(targetFhirPath);

            if (found_property.Any())
            {
                return found_property.First();
            }

            var symbolTable = new SymbolTable().AddStandardFP().AddFhirExtensions();
            var compiler = new FhirPathCompiler(symbolTable);
            var expression = compiler.Parse(targetFhirPath);
            targetFhirPath = FilterFHIRPath(expression);

            Base target = resource;

            var resource_properties = ModelInfo.ModelInspector.FindClassMapping(resource.TypeName).PropertyMappings;

            //Daniel - It looks like fhirpath works without including the resource name in the beginning. Removing this for now
            //Strip out the first entry in the fhir path (the resource). We only need to traverse through the resource properties
            //string target_properties_fhir_path = targetFhirPath.Substring(targetFhirPath.IndexOf(".") + 1);

            PropertyMapping cur_property = null;

            //Iterate through the fhir path to find if we have the entire fhirpath in memory. If not, contsruct it.
            foreach (var property in targetFhirPath.Split('.'))
            {
                //If cur_inspector_property is null, we are inspecting the properties of the class. If it is not null, we are inspecting a property within a property.
                if (cur_property == null)
                {
                    cur_property = resource_properties.Where(x => x.Name == property).First();
                }
                else
                {
                    cur_property = cur_property.PropertyTypeMapping.PropertyMappings.Where(x => x.Name == property).First();
                }

                PropertyInfo target_property_info = target.GetType().GetProperty(cur_property.NativeProperty.Name);

                switch (cur_property.FhirType.FirstOrDefault().Name)
                {
                    case nameof(CodeableConcept):
                        var codeable_concepts = new List<CodeableConcept>() { new CodeableConcept() };
                        target_property_info.SetValue(target, codeable_concepts);
                        target = codeable_concepts.FirstOrDefault();
                        break;
                    case nameof(Coding):
                        var codings = new List<Coding>() { new Coding() };
                        target_property_info.SetValue(target, codings);
                        target = codings.FirstOrDefault();
                        break;
                    case nameof(Code):
                        var code = new Code() { Value = "help!" };
                        target_property_info.SetValue(target, code);
                        target = code;
                        break;
                }
            }

            return target;
        }

        public static string FilterFHIRPath(Hl7.FhirPath.Expressions.Expression expression)
        {
            //Daniel: This may be helpful to use to validate whether or not we want operations that contain a fhirPath to have certain expressions. I'm just using it here to test stripping away any function or index related expressions within the fhir path.
            StringBuilder builder = new StringBuilder();

            while (true)
            {
                if (expression.GetType() == typeof(AxisExpression))
                {
                    break;
                }
                else if (expression.GetType() == typeof(ChildExpression))
                {
                    builder.Insert(0, ((ChildExpression)expression).ChildName + ".");
                    expression = ((ChildExpression)expression).Focus;
                }
                else if (expression.GetType() == typeof(FunctionCallExpression))
                {
                    expression = ((FunctionCallExpression)expression).Focus;
                }
                else
                {
                    expression = ((IndexerExpression)expression).Focus;
                }
            }

            return builder.ToString().TrimEnd('.');
        }
    }
}
