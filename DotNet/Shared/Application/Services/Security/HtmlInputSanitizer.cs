﻿using Ganss.Xss;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.Shared.Application.Services.Security
{
    public static class HtmlInputSanitizer
    {
        private static readonly HtmlSanitizer Sanitizer = new();

        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var sanitizedInput = Sanitizer.Sanitize(input);
            return sanitizedInput;
        }

        public static string SanitizeAndRemoveNonAlphaNumeric(string input)
        {
            var sanitizedInput = Sanitize(input);
            sanitizedInput = Regex.Replace(sanitizedInput, string.Empty, "^[a-zA-Z0-9/-]*$", RegexOptions.Compiled);
            return sanitizedInput;
        }
    }
}