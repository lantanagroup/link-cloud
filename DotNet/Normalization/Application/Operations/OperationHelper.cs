using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public static class OperationHelper
    {
        public static IOperation? GetOperation(string operrationType, string operationJson)
        {
            var operationType = OperationType.None;

            if (operrationType != null && !Enum.TryParse(operrationType, ignoreCase: true, out operationType))
            {
                return null;
            }

            object? operation = operationType switch
            {
                OperationType.CopyProperty => JsonSerializer.Deserialize<CopyPropertyOperation>(operationJson),
                OperationType.CodeMap => JsonSerializer.Deserialize<CodeMapOperation>(operationJson),
                OperationType.ConditionalTransform => JsonSerializer.Deserialize<ConditionalTransformOperation>(operationJson),
                _ => null
            };

            return (IOperation?)operation;
        }

    }
}
