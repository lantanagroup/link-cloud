using Ganss.Xss;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.Shared.Application.Services.Security
{
    public static class HtmlInputSanitizer
    {
        private static readonly HtmlSanitizer Sanitizer = new();


        /// <summary>
        /// Sanitizes user input.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var sanitizedInput = Sanitizer.Sanitize(input);
            return sanitizedInput;
        }

        /// <summary>
        /// Sanitizes user input and removes any non alpha-numeric characters, except '-', '_', and ' '.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SanitizeAndRemove(string input)
        {
            var sanitizedInput = Sanitize(input);
            sanitizedInput = Regex.Replace(sanitizedInput, @"[^a-zA-Z0-9\\-\\_ ]", string.Empty, RegexOptions.Compiled);
            return sanitizedInput;
        }
    }
}