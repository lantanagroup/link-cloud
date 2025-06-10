using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public static string ReportTrackingIdGuid { get; set; }
        public static string AdHocReportTrackingIdGuid { get; set; }
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