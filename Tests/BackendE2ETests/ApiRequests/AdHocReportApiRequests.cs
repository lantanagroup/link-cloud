using Newtonsoft.Json.Linq;
using TestHelper;
using RestSharp;
using API_Integration.Pages;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.BackendE2ETests.ApiRequests
{
    public class AdHocReportApiRequests(ITestOutputHelper output) : ApiBasePage
    {
        string AdHocReportGuid => TestContextStore.AdHocReportTrackingIdGuid;
        private void WaitForRequestComplete(int milliseconds = 1000)
        {
            Task.Delay(milliseconds).GetAwaiter().GetResult();
        }

        #region SingleMeasureAdHoc
        public void Create_SingleMeasureAdHocTestFacility()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/Facility", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"
            {
                ""FacilityId"": """ + SingleMeasureAdHocFacility + @""",
                ""FacilityName"": """ + SingleMeasureAdHocFacility + @""",
                ""TimeZone"": ""America/Chicago"",
                ""ScheduledReports"": {
                    ""monthly"": [""" + MeasureAch + @"""],
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
            var request = new RestRequest("/census/config", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
            ""facilityID"": """ + SingleMeasureAdHocFacility + @""",
            ""scheduledTrigger"": """ + CronValue + @"""
            }";

            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;
            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                output.WriteLine("Census was successfully configured.");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                output.WriteLine("ALERT - There is an existing Census for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Census was NOT successfully created. Please reauthenticate.");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Census was not successfully configured. POSTCensusConfiguration - FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasureQueryDispatchConfig_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/querydispatch/configuration", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"
                {
                  ""FacilityId"": """ + SingleMeasureAdHocFacility + @""",
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
                output.WriteLine("Config was successfully created");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                output.WriteLine("ALERT - There is an existing Config for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Config was NOT successfully created. Please reauthenticate.");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  Config was NOT successfully created. The Service is unavailable, please alert dev team.");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Config was not successfully created.");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/data/fhirQueryConfiguration/", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
            ""FacilityId"": """ + SingleMeasureAdHocFacility + @""",
            ""FhirServerBaseUrl"": """ + fhirServerBaseUrl + @""",
            ""Authentication"": {},
            ""QueryPlanIds"": [
            """ + MeasureAch + @"""
                ]
            }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;

            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK")
            {
                output.WriteLine("Query was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                output.WriteLine("ALERT - There is an existing Query Config for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Query was NOT successfully scheduled. Please reauthenticate. POST_FHIRQueryConfigByFacility FAILED");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  Query was NOT successfully scheduled. The Service is unavailable, please alert dev team. POST_FHIRQueryConfigByFacility FAILED");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Query was not successfully configured. POST_FHIRQueryConfigByFacility FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/data/{SingleMeasureAdHocFacility}/QueryPlan", Method.Post);
            request.AddHeader("Content-Type", "application/json");         
            var body = @"{
                  ""PlanName"": """ + MeasureAch + @""",
                  ""ReportType"": """ + MeasureAch + @""",
                  ""FacilityId"": """ + SingleMeasureAdHocFacility + @""",
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
                output.WriteLine("Query Plan was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                output.WriteLine("ALERT - There is an existing Query Plan for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Query Plan was NOT successfully scheduled. Please reauthenticate. POST_QueryPlanByFacility FAILED");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴 Query Plan was NOT successfully scheduled. The Service is unavailable, please alert dev team. POST_QueryPlanByFacility FAILED");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Query Plan was not successfully configured. POST_QueryPlanByFacility FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/data/{SingleMeasureAdHocFacility}/QueryPlan", Method.Post);
            request.AddHeader("Content-Type", "application/json");          
            var body = @"{
                  ""PlanName"": """ + MeasureAch + @""",
                  ""ReportType"": """ + MeasureAch + @""",
                  ""FacilityId"": """ + SingleMeasureAdHocFacility + @""",
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
                output.WriteLine("Query Plan was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                output.WriteLine("ALERT - There is an existing Query Plan for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Query Plan was NOT successfully scheduled. Please reauthenticate. POST_QueryPlanByFacility FAILED");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  Query Plan was NOT successfully scheduled. The Service is unavailable, please alert dev team. POST_QueryPlanByFacility FAILED");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Query Plan was not successfully configured. POST_QueryPlanByFacility FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasureFHIRQueryListByFacility_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/data/fhirQueryList", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
              ""facilityId"": """ + SingleMeasureAdHocFacility + @""",
              ""fhirBaseServerUrl"": """ + fhirServerBaseUrl + @""",
              ""ehrPatientLists"": [
                {
                  ""listIds"": [
                    """ + AdHocSmokeTestFile + @"""
                  ],
                        ""MeasureIds"": [
                    """ + MeasureAch + @"""
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
                output.WriteLine("Query List was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                output.WriteLine("ALERT - There is an existing Query List for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Query List was NOT successfully scheduled. Please reauthenticate. POST_FHIRQueryListByFacility FAILED");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  Query List was NOT successfully scheduled. The Service is unavailable, please alert dev team. POST_FHIRQueryListByFacility FAILED");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Query List was not successfully configured. POST_FHIRQueryListByFacility FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasureFacilityNormalizationConfig_AdHoc()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/normalization/", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
                ""FacilityId"": """ + SingleMeasureAdHocFacility + @""",
                ""OperationSequence"": {
                    ""0"": {
                        ""$type"": ""ConceptMapOperation"",
                        ""FacilityId"": """ + SingleMeasureAdHocFacility + @""",
                        ""name"": """ + SingleMeasureAdHocFacility + @""" Concept Map example"",
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
                        ""facilityId"": """ + SingleMeasureAdHocFacility + @""",
                        ""name"": ""PeriodDateFixer"",
                        ""conditions"": [],
                        ""transformResource"": """",
                        ""transformElement"": ""Period"",
                        ""transformValue"": """"
                    },
                    ""3"": {
                        ""$type"": ""ConditionalTransformationOperation"",
                        ""facilityId"": """ + SingleMeasureAdHocFacility + @""",
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
                output.WriteLine("Normalization Config was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                output.WriteLine("ALERT - There is an existing Normalization Config for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Normalization Config was NOT successfully scheduled. Please reauthenticate.");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  Normalization Config was NOT successfully scheduled. The Service is unavailable, please alert dev team.");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Normalization Config was not successfully configured.");
                Xunit.Assert.Fail();
            }
        }
        public void GenerateSingleMeasureAdHocReport_ACH()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/facility/{SingleMeasureAdHocFacility}/AdhocReport", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            
            var body = @"{
                ""BypassSubmission"": false,
                ""StartDate"": ""2025-04-01T00:00:00Z"",
                ""EndDate"": ""2025-05-20T23:59:59.99Z"",
                ""ReportTypes"": [""" + MeasureAch + @"""],
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
                output.WriteLine("AdHoc Report was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                output.WriteLine("ALERT - There is an existing AdHoc Report for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  AdHoc Report was NOT successfully scheduled. Please reauthenticate.");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  AdHoc Report was NOT successfully scheduled. The Service is unavailable, please alert dev team.");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  AdHoc Report was not successfully configured");
                Xunit.Assert.Fail();
            }
        }
        public void GETSingleMeasureAdHocSubmissionDownloadReport()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };

            var client = new RestClient(options);
            var request = new RestRequest($"/Submission/{SingleMeasureAdHocFacility}/{AdHocReportGuid}", Method.Get);        
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            JObject jsonResponse = JObject.Parse(response.Content);
            var responseCode = response.StatusCode;
            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                output.WriteLine("AdHoc report was successfully created.");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  AdHoc report was NOT created. Check to make sure you are properly authenticated.");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "BadRequest")
            {
                output.WriteLine("🔴  AdHoc report was NOT created. Please check the GETSubmissionDownloadReport request");
                Xunit.Assert.Fail();
            }
        }
        public void GETSingleMeasureAdHocFacilityValidationResultsForReport()
        {
            var options = new RestClientOptions(api_LinkAdminBffURL)
            {
                MaxTimeout = -1,
            };

            var client = new RestClient(options);
            var request = new RestRequest($"/validation/result/{SingleMeasureAdHocFacility}/{AdHocReportGuid}", Method.Get);         
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
                            output.WriteLine("[INFO] JSON response parsed as JObject.");
                        }
                        else if (content.StartsWith("["))
                        {
                            JArray jsonArrayResponse = JArray.Parse(content);
                            output.WriteLine("[INFO] JSON response parsed as JArray.");
                        }
                        else
                        {
                            output.WriteLine("[WARNING] Response is not valid JSON.");
                        }
                    }
                    catch (Exception ex)
                    {
                        output.WriteLine($"[WARNING] Failed to parse JSON: {ex.Message}");
                    }
                }
                output.WriteLine("[PASS] Validation report was successfully retrieved.");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("[ERROR] The Get Validation Report request was NOT successful. Authentication failed.");
                Xunit.Assert.Fail("Unauthorized request.");
            }
            if (responseCodeString == "BadRequest")
            {
                output.WriteLine("[ERROR] The Get Validation Report request was NOT successful. Please verify the request parameters.");
                Xunit.Assert.Fail("Bad request.");
            }
            output.WriteLine($"[ERROR] Unexpected response: {responseCodeString}");
            Xunit.Assert.Fail($"Unexpected validation report response: {responseCodeString}");
        }
        #endregion
    }
}
