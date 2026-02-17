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
        /// Cleans an untrusted string by replacing control characters with spaces.
        /// This method helps prevent log injection attacks by removing control characters including
        /// newlines (CR/LF) that could be used to forge log entries.
        /// </summary>
        /// <param name="originalString">The string to clean</param>
        /// <returns>A cleaned string with printable ASCII (32-255) preserved. 
        /// Control characters (0-31) and DEL (127) are replaced with spaces.</returns>
        /// <remarks>
        /// Security: Removes control characters (0-31, 127) that could be used for log injection.
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
            // This explicitly excludes:
            // - Control characters (0-31) including tab, newline, carriage return
            // - DEL character (127)
            // This must done in a loop so that Fortify can recognize the control character check and not flag this as a potential log injection vulnerability.
            for (int i = 32; i <= 255; i++)
            {
                if (aChar == i && aChar != 127) return (char)i;
            }
            return ' ';
        }
    }
}