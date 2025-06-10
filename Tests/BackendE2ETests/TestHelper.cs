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







    public class UserRole
    {
        public readonly string CancellationApprover = "Cancellation Approver (CRD or Standard)";
        public readonly string CancellationSubmitter = "Cancellation Submitter";
        public readonly string Administrator = "Administrator";
        public readonly string ClaimsApprover = "Claims Approver";
        public readonly string ClaimsAdministrator = "Claims Administrator";
        public readonly string ClaimsAdministratorCanada = "Claims Administrator Canada";
        public readonly string ClaimsApproverCanada = "Claims Approver Canada";
        public readonly string ClaimsManager = "Claims Manager";
        public readonly string ClaimsManagerCanada = "Claims Manager Canada";
        public readonly string Director = "Director";
        public readonly string DirectorCanada = "Director Canada";
        public readonly string PricingManager = "Pricing Manager";
        public readonly string PricingManagerCanada = "Pricing Manager Canada";
        public readonly string ProgramAdminCancellations = "Program Admin - CXL";
        public readonly string ProgramAdminSalesPrograms = "Program Admin - SP";
        public readonly string ReadOnly = "Read Only";
        public readonly string ReadOnlyBudgets = "Read Only Budgets";
        public readonly string ReadOnlyBudgetsCanada = "Read Only Budgets Canada";
        public readonly string SalesManager = "Sales Manager";
        public readonly string SalesManagerCanada = "Sales Manager Canada";
        public readonly string SalesRep = "Sales Rep";
        public readonly string SalesRepCanada = "Sales Rep Canada";
        public readonly string SalesProgramsCreateProgram = "SP Create Program";
        public readonly string SalesProgramsCreateProgramCanada = "SP Create Program - Canada";
        public readonly string SalesProgramsManageDiscounts = "SP Manage Discounts";
        public readonly string SalesProgramsViewProgram = "SP View Program";
        public readonly string SalesProgramsViewProgramCanada = "SP View Program - Canada";
    }
    public class UserUpdateObject
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Region { get; set; }
        public List<string> Roles { get; set; }
        public string Aliases { get; set; }
        public DateTime LastActivity { get; set; }
        public string ApplicationName { get; set; }
        public Nullable<int> RepNumber { get; set; }
        public string DisplayName { get; set; }
        public bool HasProfile { get; set; }
        public bool ProfileUpdated { get; set; }
        public bool IsActive { get; set; }
        public bool IsActiveDirectoryUser { get; set; }
        public bool IsDirty { get; set; }







    }

}
