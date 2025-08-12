namespace LantanaGroup.Link.DataAcquisition.Domain.Extensions;
public static class PatientIdExtensions
{
    public static string RemoveIdPathParts(this string fullResourceUrl)
    {
        var separatedResourceUrl = fullResourceUrl.Split('/');
        var resourceIdPart = string.Join("/", separatedResourceUrl.Skip(Math.Max(0, separatedResourceUrl.Length - 2)));
        return resourceIdPart;
    }
}
