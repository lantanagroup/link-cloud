using System.Text;

namespace LantanaGroup.Link.Shared.Application.Services.Security
{
    public static class StringSecurityExtensions
    {
        private static readonly IDictionary<char, char> SafeCharacters;

        static StringSecurityExtensions()
        {
            SafeCharacters = new Dictionary<char, char>();
            for (char ch = (char)32; ch <= 255; ch++)
            {
                if (char.IsControl(ch))
                {
                    continue;
                }
                SafeCharacters[ch] = ch;
            }
        }

        /// <summary>
        /// Cleans an untrusted string by replacing control characters with spaces.
        /// This method helps prevent log injection attacks by removing control characters including
        /// newlines (CR/LF) that could be used to forge log entries.
        /// </summary>
        /// <param name="obj">The object whose string representation will be sanitized</param>
        /// <returns>A cleaned string with printable Latin-1 characters preserved.
        /// Control characters, including DEL and C1 controls, are replaced with spaces.</returns>
        /// <remarks>
        /// Characters outside the printable Latin-1 range are also replaced with spaces.
        /// </remarks>
        public static string SanitizeForLog(this object? obj)
        {
            if (obj is null) return null;
            string? originalString = obj.ToString();
            if (originalString is null) return null;
            StringBuilder builder = new(originalString.Length);
            foreach (char ch in originalString)
            {
                builder.Append(cleanChar(ch));
            }
            return builder.ToString();
        }



        private static char cleanChar(char aChar)
        {
            // This explicitly excludes:
            // - Control characters (0-31) including tab, newline, carriage return
            // - DEL and C1 control characters (127-159), including NEXT LINE (133)
            return SafeCharacters.TryGetValue(aChar, out char safeCharacter) ? safeCharacter : ' ';
        }

        /// <summary>
        /// Masks a potentially sensitive string for logging by sanitizing control
        /// characters and fully redacting the value. Null/empty values return an empty string.
        /// </summary>
        /// <remarks>
        /// Security: Prevents PII/PHI leakage via log output by ensuring no cleartext
        /// characters from the original value are emitted.
        /// </remarks>
        public static string MaskForLog(this string? value)
        {
            var sanitized = value.SanitizeForLog();
            if (string.IsNullOrEmpty(sanitized))
                return string.Empty;
            return new string('*', sanitized.Length);
        }
    }
}
