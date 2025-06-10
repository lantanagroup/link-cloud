using System;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestHelper;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Configuration;
using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Extensions;
using RestSharp.Serializers;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System.Diagnostics;
using System.Net.Http;
using OpenQA.Selenium.BiDi.Modules.Network;
using Hl7.FhirPath.Sprache;
using API_Integration.Pages;

namespace LantanaGroup.Link.Tests.BackendE2ETests.Pages_Services
{
    public class AdHocReportApiRequests : ApiBasePage
    {
        string AdHocReportGuid => TestContextStore.AdHocReportTrackingIdGuid;
        public TestContext TestContext { get; set; }
        private void WaitForRequestComplete(int milliseconds = 1000)
        {
            Task.Delay(milliseconds).GetAwaiter().GetResult();
        }


        #region Common Functions
        public void UpdateMeasureDefinition()
         {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/api/measure-definition/{measureACH}", Method.Put);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", "<file contents here>", ParameterType.RequestBody);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            TestContext?.WriteLine("Measure Definition was Run. This does NOT mean it was replaced or added to the server. Please check that desired measure exists for Facility.");
         }
        #endregion

        #region SingleMeasureAdHoc
        public void Create_SingleMeasureAdHocTestFacility()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/api/Facility", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"
            {
                ""FacilityId"": """ + singleMeasureAdHocFacility + @""",
                ""FacilityName"": """ + singleMeasureAdHocFacility + @""",
                ""TimeZone"": ""America/Chicago"",
                ""ScheduledReports"": {
                    ""monthly"": [""" + measureACH + @"""],
                    ""daily"": [],
                    ""weekly"": []
                }
            }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
        }
        public void Create_SingleMeasureCensusConfiguration_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/api/census/config", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            

            var body = @"{
            ""facilityID"": """ + singleMeasureAdHocFacility + @""",
            ""scheduledTrigger"": """ + cronValue + @"""
            }";

            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;
            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                TestContext?.WriteLine("Census was successfully configured.");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                TestContext?.WriteLine("ALERT - There is an existing Census for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("Census was NOT successfully created. Please reauthenticate.");
                Assert.Fail();
            }
            else
            {
                TestContext?.WriteLine("Census was not successfully configured. POSTCensusConfiguration - FAILED");
                Assert.Fail();
            }
        }
        public void Create_SingleMeasureQueryDispatchConfig_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/api/querydispatch/configuration", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"
                {
                  ""FacilityId"": """ + singleMeasureAdHocFacility + @""",
                  ""DispatchSchedules"": [
                    {
                      ""Event"": ""Discharge"",
                      ""Duration"": ""PT10S""
                    }
                  ]
                }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;

            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK")
            {
                TestContext?.WriteLine("Config was successfully created");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                TestContext?.WriteLine("ALERT - There is an existing Config for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("Config was NOT successfully created. Please reauthenticate.");
                Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                TestContext?.WriteLine("Config was NOT successfully created. The Service is unavailable, please alert dev team.");
                Assert.Fail();
            }
            else
            {
                TestContext?.WriteLine("Config was not successfully created.");
                Assert.Fail();
            }
        }
        public void Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/api/data/fhirQueryConfiguration/", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            

            var body = @"{
            ""FacilityId"": """ + singleMeasureAdHocFacility + @""",
            ""FhirServerBaseUrl"": """ + fhirServerBaseUrl + @""",
            ""Authentication"": {},
            ""QueryPlanIds"": [
            """ + measureACH + @"""
                ]
            }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;

            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK")
            {
                TestContext?.WriteLine("Query was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                TestContext?.WriteLine("ALERT - There is an existing Query Config for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("Query was NOT successfully scheduled. Please reauthenticate. POST_FHIRQueryConfigByFacility FAILED");
                Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                TestContext?.WriteLine("Query was NOT successfully scheduled. The Service is unavailable, please alert dev team. POST_FHIRQueryConfigByFacility FAILED");
                Assert.Fail();
            }
            else
            {
                TestContext?.WriteLine("Query was not successfully configured. POST_FHIRQueryConfigByFacility FAILED");
                Assert.Fail();
            }
        }
        public void Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/api/data/{singleMeasureAdHocFacility}/QueryPlan", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
                  ""PlanName"": """ + measureACH + @""",
                  ""ReportType"": """ + measureACH + @""",
                  ""FacilityId"": """ + singleMeasureAdHocFacility + @""",
                  ""EHRDescription"": ""Epic"",
                  ""LookBack"": ""P0D"",
                  ""Type"": ""Monthly"",
                  ""InitialQueries"": {
                    ""0"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Encounter"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""1"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Location"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    }
                  },
                  ""SupplementalQueries"": {
                    ""0"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Condition"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain"",
                          ""Name"": ""encounter"",
                          ""Resource"": ""Encounter"",
                          ""Paged"": ""100""
                        }
                      ]
                    },
                    ""1"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Coverage"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        }
                      ]
                    },
                    ""2"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Observation"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.LiteralParameter, DataAcquisition.Domain"",
                          ""Name"": ""category"",
                          ""Literal"": ""imaging,laboratory,social-history,vital-signs""
                        }
                      ]
                    },
                    ""3"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Procedure"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""4"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""ServiceRequest"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain"",
                          ""Name"": ""encounter"",
                          ""Resource"": ""Encounter"",
                          ""Paged"": ""100""
                        }
                      ]
                    },
                    ""5"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""DiagnosticReport"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""6"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""MedicationRequest"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""authoredon"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""authoredon"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.LiteralParameter, DataAcquisition.Domain"",
                          ""Name"": ""intent"",
                          ""Literal"": ""order""
                        }
                      ]
                    },
                    ""7"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Medication"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    },
                    ""8"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Specimen"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    },
                    ""9"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Device"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    }
                  }
                }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;

            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                TestContext?.WriteLine("Query Plan was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                TestContext?.WriteLine("ALERT - There is an existing Query Plan for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("Query Plan was NOT successfully scheduled. Please reauthenticate. POST_QueryPlanByFacility FAILED");
                Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                TestContext?.WriteLine("Query Plan was NOT successfully scheduled. The Service is unavailable, please alert dev team. POST_QueryPlanByFacility FAILED");
                Assert.Fail();
            }
            else
            {
                TestContext?.WriteLine("Query Plan was not successfully configured. POST_QueryPlanByFacility FAILED");
                Assert.Fail();
            }
        }
        public void Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/api/data/{singleMeasureAdHocFacility}/QueryPlan", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
                  ""PlanName"": """ + measureACH + @""",
                  ""ReportType"": """ + measureACH + @""",
                  ""FacilityId"": """ + singleMeasureAdHocFacility + @""",
                  ""EHRDescription"": ""Epic"",
                  ""LookBack"": ""P0D"",
                  ""Type"": ""Discharge"",
                  ""InitialQueries"": {
                    ""0"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Encounter"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""1"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Location"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    }
                  },
                  ""SupplementalQueries"": {
                    ""0"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Condition"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain"",
                          ""Name"": ""encounter"",
                          ""Resource"": ""Encounter"",
                          ""Paged"": ""100""
                        }
                      ]
                    },
                    ""1"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Coverage"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        }
                      ]
                    },
                    ""2"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Observation"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.LiteralParameter, DataAcquisition.Domain"",
                          ""Name"": ""category"",
                          ""Literal"": ""imaging,laboratory,social-history,vital-signs""
                        }
                      ]
                    },
                    ""3"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Procedure"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""4"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""ServiceRequest"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain"",
                          ""Name"": ""encounter"",
                          ""Resource"": ""Encounter"",
                          ""Paged"": ""100""
                        }
                      ]
                    },
                    ""5"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""DiagnosticReport"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""6"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""MedicationRequest"",
                      ""Parameters"": [
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""authoredon"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain"",
                          ""Name"": ""authoredon"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        },
                        {
                          ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.LiteralParameter, DataAcquisition.Domain"",
                          ""Name"": ""intent"",
                          ""Literal"": ""order""
                        }
                      ]
                    },
                    ""7"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Medication"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    },
                    ""8"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Specimen"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    },
                    ""9"": {
                      ""$type"": ""LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain"",
                      ""ResourceType"": ""Device"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    }
                  }
                }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;

            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                TestContext?.WriteLine("Query Plan was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                TestContext?.WriteLine("ALERT - There is an existing Query Plan for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("Query Plan was NOT successfully scheduled. Please reauthenticate. POST_QueryPlanByFacility FAILED");
                Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                TestContext?.WriteLine("Query Plan was NOT successfully scheduled. The Service is unavailable, please alert dev team. POST_QueryPlanByFacility FAILED");
                Assert.Fail();
            }
            else
            {
                TestContext?.WriteLine("Query Plan was not successfully configured. POST_QueryPlanByFacility FAILED");
                Assert.Fail();
            }
        }
        public void Create_SingleMeasureFHIRQueryListByFacility_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/api/data/fhirQueryList", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
              ""facilityId"": """ + singleMeasureAdHocFacility + @""",
              ""fhirBaseServerUrl"": """ + fhirServerBaseUrl + @""",
              ""ehrPatientLists"": [
                {
                  ""listIds"": [
                    """ + adHocSmokeTestFile + @"""
                  ],
                        ""MeasureIds"": [
                    """ + measureACH + @"""
                  ]
                }
              ]
            }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;

            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK")
            {
                TestContext?.WriteLine("Query List was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                TestContext?.WriteLine("ALERT - There is an existing Query List for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("Query List was NOT successfully scheduled. Please reauthenticate. POST_FHIRQueryListByFacility FAILED");
                Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                TestContext?.WriteLine("Query List was NOT successfully scheduled. The Service is unavailable, please alert dev team. POST_FHIRQueryListByFacility FAILED");
                Assert.Fail();
            }
            else
            {
                TestContext?.WriteLine("Query List was not successfully configured. POST_FHIRQueryListByFacility FAILED");
                Assert.Fail();
            }
        }
        public void Create_SingleMeasureFacilityNormalizationConfig_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/api/normalization/", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
                ""FacilityId"": """ + singleMeasureAdHocFacility + @""",
                ""OperationSequence"": {
                    ""0"": {
                        ""$type"": ""ConceptMapOperation"",
                        ""FacilityId"": """ + singleMeasureAdHocFacility + @""",
                        ""name"": """ + singleMeasureAdHocFacility + @""" Concept Map example"",
                        ""FhirConceptMap"": {
                            ""resourceType"": ""ConceptMap"",
                            ""id"": ""ehr-test-epic-encounter-class"",
                            ""url"": ""https://nhsnlink.org/fhir/ConceptMap/ehr-test-epic-encounter-class"",
                            ""identifier"": {
                                ""system"": ""urn:ietf:rfc:3986"",
                                ""value"": ""urn:uuid:63cd62ee-033e-414c-9f58-3ca97b5ffc3b""
                            },
                            ""version"": ""20220728"",
                            ""name"": ""ehr-test-epic-encounter-class"",
                            ""title"": ""Ehr-test Epic Encounter Class ConceptMap"",
                            ""status"": ""draft"",
                            ""experimental"": true,
                            ""date"": ""2022-07-28"",
                            ""description"": ""A mapping between the Epic's Encounter class codes and HL7 v3-ActEncounter codes"",
                            ""purpose"": ""To help implementers map from University of Michigan Epic to FHIR"",
                            ""group"": [
                                {
                                    ""source"": ""urn:oid:1.2.840.114350.1.72.1.7.7.10.696784.13260"",
                                    ""target"": ""http://terminology.hl7.org/CodeSystem/v3-ActCode"",
                                    ""element"": [
                                        {
                                            ""code"": ""1"",
                                            ""target"": [
                                                {
                                                    ""code"": ""IMP"",
                                                    ""display"": ""inpatient"",
                                                    ""equivalence"": ""inexact""
                                                }
                                            ]
                                        },
                                        {
                                            ""code"": ""2"",
                                            ""target"": [
                                                {
                                                    ""code"": ""IMP"",
                                                    ""display"": ""inpatient"",
                                                    ""equivalence"": ""inexact""
                                                }
                                            ]
                                        },
                                        {
                                            ""code"": ""3"",
                                            ""target"": [
                                                {
                                                    ""code"": ""IMP"",
                                                    ""display"": ""inpatient"",
                                                    ""equivalence"": ""inexact""
                                                }
                                            ]
                                        },
                                        {
                                            ""code"": ""4"",
                                            ""target"": [
                                                {
                                                    ""code"": ""IMP"",
                                                    ""display"": ""inpatient"",
                                                    ""equivalence"": ""inexact""
                                                }
                                            ]
                                        },
                                        {
                                            ""code"": ""5"",
                                            ""target"": [
                                                {
                                                    ""code"": ""IMP"",
                                                    ""display"": ""inpatient"",
                                                    ""equivalence"": ""inexact""
                                                }
                                            ]
                                        },
                                        {
                                            ""code"": ""6"",
                                            ""target"": [
                                                {
                                                    ""code"": ""IMP"",
                                                    ""display"": ""inpatient"",
                                                    ""equivalence"": ""inexact""
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        },
                        ""FhirPath"": null,
                       ""FhirContext"": ""Encounter""
                    },
                    ""1"": {
                        ""$type"": ""CopyLocationIdentifierToTypeOperation"",
                        ""name"": ""Test Location Type""
                    },
                    ""2"": {
                        ""$type"": ""ConditionalTransformationOperation"",
                        ""facilityId"": """ + singleMeasureAdHocFacility + @""",
                        ""name"": ""PeriodDateFixer"",
                        ""conditions"": [],
                        ""transformResource"": """",
                        ""transformElement"": ""Period"",
                        ""transformValue"": """"
                    },
                    ""3"": {
                        ""$type"": ""ConditionalTransformationOperation"",
                        ""facilityId"": """ + singleMeasureAdHocFacility + @""",
                        ""name"": ""EncounterStatusTransformation"",
                        ""conditions"": [],
                        ""transformResource"": ""Encounter"",
                        ""transformElement"": ""Status"",
                        ""transformValue"": """"
                    }
                }
            }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;

            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK")
            {
                TestContext?.WriteLine("Normalization Config was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                TestContext?.WriteLine("ALERT - There is an existing Normalization Config for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("Normalization Config was NOT successfully scheduled. Please reauthenticate.");
                Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                TestContext?.WriteLine("Normalization Config was NOT successfully scheduled. The Service is unavailable, please alert dev team.");
                Assert.Fail();
            }
            else
            {
                TestContext?.WriteLine("Normalization Config was not successfully configured.");
                Assert.Fail();
            }
        }
        public void GenerateSingleMeasureAdHocReport_ACH()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/api/facility/{singleMeasureAdHocFacility}/AdhocReport", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
                ""BypassSubmission"": false,
                ""StartDate"": ""2025-04-01T00:00:00Z"",
                ""EndDate"": ""2025-05-20T23:59:59.99Z"",
                ""ReportTypes"": [""" + measureACH + @"""],
                ""PatientIds"": [""Patient-multi6"", ""Patient-multi10"", ""Patient-May1"", ""Patient-HYPOAPR2"", ""Patient-HYPOAPR1"", ""Patient-multi8"", ""Patient-multi9"", ""Patient-June1""]
            }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            string reportGuid = response.Content;
            JObject json = JObject.Parse(reportGuid);
            string reportIdGuid = (string)json["reportId"];

            TestContextStore.AdHocReportTrackingIdGuid = reportIdGuid;

            var responseCode = response.StatusCode;
            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK")
            {
                TestContext?.WriteLine("AdHoc Report was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                TestContext?.WriteLine("ALERT - There is an existing AdHoc Report for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("AdHoc Report was NOT successfully scheduled. Please reauthenticate.");
                Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                TestContext?.WriteLine("AdHoc Report was NOT successfully scheduled. The Service is unavailable, please alert dev team.");
                Assert.Fail();
            }
            else
            {
                TestContext?.WriteLine("AdHoc Report was not successfully configured");
                Assert.Fail();
            }
        }
        public void GETSingleMeasureAdHocSubmissionDownloadReport()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };

            var client = new RestClient(options);
            var request = new RestRequest($"/api/Submission/{singleMeasureAdHocFacility}/{AdHocReportGuid}", Method.Get);
            
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();

            JObject jsonResponse = JObject.Parse(response.Content);
            var responseCode = response.StatusCode;
            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                TestContext?.WriteLine("AdHoc report was successfully created.");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("AdHoc report was NOT created. Check to make sure you are properly authenticated.");
                Assert.Fail();
            }
            if (responseCodeString == "BadRequest")
            {
                TestContext?.WriteLine("AdHoc report was NOT created. Please check the GETSubmissionDownloadReport request");
                Assert.Fail();
            }
        }
        public void GETSingleMeasureAdHocFacilityValidationResultsForReport()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };

            var client = new RestClient(options);
            var request = new RestRequest($"/api/validation/result/{singleMeasureAdHocFacility}/{AdHocReportGuid}", Method.Get);
            

            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();

            var responseCode = response.StatusCode;
            string responseCodeString = responseCode.ToString();

            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                string content = response.Content?.Trim();

                if (!string.IsNullOrEmpty(content))
                {
                    try
                    {
                        if (content.StartsWith("{"))
                        {
                            JObject jsonResponse = JObject.Parse(content);
                            TestContext?.WriteLine("[INFO] JSON response parsed as JObject.");
                        }
                        else if (content.StartsWith("["))
                        {
                            JArray jsonArrayResponse = JArray.Parse(content);
                            TestContext?.WriteLine("[INFO] JSON response parsed as JArray.");
                        }
                        else
                        {
                            TestContext?.WriteLine("[WARNING] Response is not valid JSON.");
                        }
                    }
                    catch (Exception ex)
                    {
                        TestContext?.WriteLine($"[WARNING] Failed to parse JSON: {ex.Message}");
                    }
                }
                TestContext?.WriteLine("[PASS] Validation report was successfully retrieved.");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                TestContext?.WriteLine("[ERROR] The Get Validation Report request was NOT successful. Authentication failed.");
                Assert.Fail("Unauthorized request.");
            }
            if (responseCodeString == "BadRequest")
            {
                TestContext?.WriteLine("[ERROR] The Get Validation Report request was NOT successful. Please verify the request parameters.");
                Assert.Fail("Bad request.");
            }
            TestContext?.WriteLine($"[ERROR] Unexpected response: {responseCodeString}");
            Assert.Fail($"Unexpected validation report response: {responseCodeString}");
        }
        #endregion

    }
}
