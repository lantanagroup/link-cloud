using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Diagnostics;
using RestSharp.Authenticators.OAuth2;
using Microsoft.Identity.Client;
using static System.Net.WebRequestMethods;
using LantanaGroup.Link.Tests.E2ETests;

namespace API_Integration.Pages
{
    public class ApiBasePage
    {
        protected static readonly string api_LinkAdminBffURL = TestConfig.AdminBffBase;
        public string adHocSmokeTestFile = "Stu3-AdHocSmokeTest";
        public string singleMeasureAdHocFacility = "SingleMeasureAdHocFacility";  
        public string singleMeasureAdHocACHdQMVersion = "0.0.014"; 
        public string measureACH = "NHSNdQMAcuteCareHospitalInitialPopulation";  
        protected static readonly string fhirServerBaseUrl = TestConfig.InternalFhirServerBase;
        public string cronValue = "0 0 */4 * * ?";  
    }
}
