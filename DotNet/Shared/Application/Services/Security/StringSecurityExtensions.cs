using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Services.Security
{
    public static class StringSecurityExtensions
    {
        /// <summary>
        /// Cleans an untrusted string by replacing non-printable ASCII characters with spaces.
        /// This method helps prevent log injection attacks by removing control characters including
        /// newlines (CR/LF) that could be used to forge log entries.
        /// </summary>
        /// <param name="originalString">The string to clean</param>
        /// <returns>A cleaned string with only printable ASCII characters (32-126). 
        /// Non-printable characters and non-ASCII Unicode characters are replaced with spaces.</returns>
        /// <remarks>
        /// Security: Removes control characters (0-31, 127+) that could be used for log injection.
        /// Note: Non-ASCII Unicode characters are replaced with spaces, which may result in data loss
        /// for internationalized content.
        /// </remarks>
        public static String SanitizeUntrustedString(this String originalString)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < originalString.Length; ++i)
            {
                builder.Append(cleanChar(originalString[i]));
            }
            return builder.ToString();
        }



        private static char cleanChar(char aChar)
        {
            // Printable ASCII characters are in the range 32-126
            // This includes space (32) through tilde (126)
            // This explicitly excludes:
            // - Control characters (0-31) including tab, newline, carriage return
            // - DEL character (127)
            // - Extended ASCII and Unicode (128+)
            for (int i = 32; i <= 126; i++)
            {
                if (aChar == i) return (char)i;
            }
            return ' ';
        }
    }
}