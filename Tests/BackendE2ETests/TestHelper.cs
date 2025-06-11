using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Serialization;
using Newtonsoft.Json;
using System.Net.Http;
using OpenQA.Selenium.BiDi.Communication;


namespace TestHelper
{
    public static class TestContextStore
    {
        private static readonly AsyncLocal<string?> _reportTrackingIdGuid = new();
        private static readonly AsyncLocal<string?> _adHocReportTrackingIdGuid = new();

        public static string? ReportTrackingIdGuid
        {
            get => _reportTrackingIdGuid.Value;
            set => _reportTrackingIdGuid.Value = value;
        }

        public static string? AdHocReportTrackingIdGuid
        {
            get => _adHocReportTrackingIdGuid.Value;
            set => _adHocReportTrackingIdGuid.Value = value;
        }
    }
    public static class ValidationHelper
    {
        /// <summary>
        /// Attempts to run a validation method. Captures and logs any exception, but does not halt the test.
        /// </summary>
        public static void TryRunValidation(Action validationMethod, List<string> failures)
        {
            try
            {
                validationMethod();
            }
            catch (Exception ex)
            {
                string methodName = validationMethod.Method.Name;
                Console.WriteLine($"[FAIL] {methodName} - {ex.Message}");
                failures.Add($"{methodName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Async version for use with asynchronous validations.
        /// </summary>
        public static async Task TryRunValidationAsync(Func<Task> validationMethod, List<string> failures)
        {
            try
            {
                await validationMethod();
            }
            catch (Exception ex)
            {
                string methodName = validationMethod.Method.Name;
                Console.WriteLine($"[FAIL] {methodName} - {ex.Message}");
                failures.Add($"{methodName}: {ex.Message}");
            }
        }
    }
}