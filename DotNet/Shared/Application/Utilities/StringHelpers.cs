namespace LantanaGroup.Link.Shared.Application.Utilities;
public static class StringHelpers
{
    public static string SplitReference(this string reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return string.Empty;
        }

        var splitReference = reference.Split("/");
        return splitReference[splitReference.Length - 1];
    }
}