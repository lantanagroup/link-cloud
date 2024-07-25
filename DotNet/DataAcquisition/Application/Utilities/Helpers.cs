namespace LantanaGroup.Link.DataAcquisition.Application.Utilities;

public static class Helpers
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
