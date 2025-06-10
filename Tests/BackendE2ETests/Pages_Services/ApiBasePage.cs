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

namespace API_Integration.Pages
{
    public class ApiBasePage
    {
        public WebDriverWait driverWait;
        public string api_LinkAdminBffURL = "http://localhost:8063";   //where to reference?
        public string adHocSmokeTestFile = "Stu3-AdHocSmokeTest";  //where to store?
        public string singleMeasureAdHocFacility = "SingleMeasureAdHocFacility";  //where to store?
        public string singleMeasureAdHocACHdQMVersion = "0.0.014"; //where to store?
        public string measureACH = "NHSNdQMAcuteCareHospitalInitialPopulation";  //where to store?
        public string fhirServerBaseUrl = "https://ehr-test.nhsnlink.org/fhir";   //where to reference?
        public string cronValue = "0 0 */4 * * ?";  //where to store?
    }
}
