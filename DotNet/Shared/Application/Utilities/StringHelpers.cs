using AngleSharp.Css.Values;
using Hl7.FhirPath.Sprache;
using System.Security.Cryptography;
using System.Text;

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

    public static long GetStableHashCode64(this string str)
    {
        if (str == null)
            throw new ArgumentNullException(nameof(str));

        unchecked
        {
            long hash1 = 5381L;
            long hash2 = hash1;

            for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0')
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            // Combine into a 64-bit result
            return (hash1 << 32) | (hash2 & 0xFFFFFFFFL);
        }
    }
}