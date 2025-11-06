using LantanaGroup.Link.Tests.E2ETests;
using Newtonsoft.Json.Linq;
using RestSharp;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.BackendE2ETests.ApiRequests
{
    public class AdHocReportApiRequests(ITestOutputHelper output)
    {
        string AdHocReportGuid => TestConfig.TestContextStore.AdHocReportTrackingIdGuid;
        public void WaitForRequestComplete(int milliseconds = 1500)
        {
            Task.Delay(milliseconds).GetAwaiter().GetResult();
        }

        #region SingleMeasureAdHoc
        public void Create_SingleMeasureAdHocTestFacility()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/Facility", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            var body = @"
            {
                ""FacilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
                ""FacilityName"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
                ""TimeZone"": ""America/Chicago"",
                ""ScheduledReports"": {
                    ""monthly"": [""" + TestConfig.MeasureAch + @"""],
                    ""daily"": [],
                    ""weekly"": []
                }
            }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();

            var responseCode = response.StatusCode;
            string responseCodeString = responseCode.ToString();
            string responseContent = response.Content;

            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                output.WriteLine("Facility was successfully created.");
                return;
            }
            if (responseCodeString == "BadRequest" && responseContent.Contains($"Facility {TestConfig.SingleMeasureAdHocFacility} already exists"))
            {
                output.WriteLine("Facility was successfully created.");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                output.WriteLine("ALERT - Facility already exists");
                return;            
            }
            else
            {
                output.WriteLine("🔴  Facility was not successfully created. Create_SingleMeasureAdHocTestFacility() - FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasureCensusConfiguration_AdHoc()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/census/config", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            var body = @"{
            ""facilityID"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
            ""scheduledTrigger"": """ + TestConfig.CronValue + @""",
            ""enabled"": false
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
                output.WriteLine("ALERT - Create_SingleMeasureCensusConfiguration_AdHoc() - There is an existing Census for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Census was NOT successfully created. Create_SingleMeasureCensusConfiguration_AdHoc() - Please reauthenticate.");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Census was not successfully configured. Create_SingleMeasureCensusConfiguration_AdHoc() - FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasureQueryDispatchConfig_AdHoc()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/querydispatch/configuration", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            var body = @"
                {
                  ""FacilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
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
            if (responseCodeString == "Created")
            {
                output.WriteLine("Config was successfully created");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                output.WriteLine("ALERT - Create_SingleMeasureQueryDispatchConfig_AdHoc() - There is an existing Config for this facility");
                return;
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  Config was NOT successfully created. Create_SingleMeasureQueryDispatchConfig_AdHoc() - The Service is unavailable, please alert dev team.");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Config was not successfully created. Create_SingleMeasureQueryDispatchConfig_AdHoc() - FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/data/fhirQueryConfiguration/", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            var body = @"{
            ""FacilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
            ""FhirServerBaseUrl"": """ + TestConfig.InternalFhirServerBase + @""",
            ""Authentication"": null,
            ""QueryPlanIds"": [
            """ + TestConfig.MeasureAch + @"""
                ],
            ""MaxConcurrentRequests"": 2,
            ""TimeZone"": ""America/Chicago""
            }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            var responseCode = response.StatusCode;

            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                output.WriteLine("Query was successfully configured");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                output.WriteLine("ALERT - There is an existing Query Config for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Query was NOT successfully configured. Please reauthenticate. Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  Query was NOT successfully configured. The Service is unavailable, please alert dev team. Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Query was not successfully configured. Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/data/{TestConfig.SingleMeasureAdHocFacility}/QueryPlan", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            var body = @"{
                  ""PlanName"": """ + TestConfig.MeasureAch + @""",
                  ""ReportType"": """ + TestConfig.MeasureAch + @""",
                  ""FacilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
                  ""EHRDescription"": ""Epic"",
                  ""LookBack"": ""P0D"",
                  ""Type"": ""Monthly"",
                  ""InitialQueries"": {
                    ""0"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Encounter"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""1"": {
                      ""QueryConfigType"": ""Reference"",
                      ""ResourceType"": ""Location"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    }
                  },
                  ""SupplementalQueries"": {
                    ""0"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Condition"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""ResourceIds"",
                          ""Name"": ""encounter"",
                          ""Resource"": ""Encounter"",
                          ""Paged"": ""100""
                        }
                      ]
                    },
                    ""1"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Coverage"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        }
                      ]
                    },
                    ""2"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Observation"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        },
                        {
                          ""ParameterType"": ""Literal"",
                          ""Name"": ""category"",
                          ""Literal"": ""imaging,laboratory,social-history,vital-signs""
                        }
                      ]
                    },
                    ""3"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Procedure"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""4"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""ServiceRequest"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""ResourceIds"",
                          ""Name"": ""encounter"",
                          ""Resource"": ""Encounter"",
                          ""Paged"": ""100""
                        }
                      ]
                    },
                    ""5"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""DiagnosticReport"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""6"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""MedicationRequest"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""authoredon"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""authoredon"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        },
                        {
                          ""ParameterType"": ""Literal"",
                          ""Name"": ""intent"",
                          ""Literal"": ""order""
                        }
                      ]
                    },
                    ""7"": {
                      ""QueryConfigType"": ""Reference"",
                      ""ResourceType"": ""Medication"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    },
                    ""8"": {
                      ""QueryConfigType"": ""Reference"",
                      ""ResourceType"": ""Specimen"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    },
                    ""9"": {
                      ""QueryConfigType"": ""Reference"",
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
                output.WriteLine("MONTHLY Query Plan was successfully created");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                output.WriteLine("ALERT - Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc() - There is an existing MONTHLY Query Plan for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  MONTHLY Query Plan was NOT successfully created. Please reauthenticate. Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴 MONTHLY Query Plan was NOT successfully created. The Service is unavailable, please alert dev team. Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  MONTHLY Query Plan was not successfully created. Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/data/{TestConfig.SingleMeasureAdHocFacility}/QueryPlan", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            var body = @"{
                  ""PlanName"": """ + TestConfig.MeasureAch + @""",
                  ""ReportType"": """ + TestConfig.MeasureAch + @""",
                  ""FacilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
                  ""EHRDescription"": ""Epic"",
                  ""LookBack"": ""P0D"",
                  ""Type"": ""Discharge"",
                  ""InitialQueries"": {
                    ""0"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Encounter"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""1"": {
                      ""QueryConfigType"": ""Reference"",
                      ""ResourceType"": ""Location"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    }
                  },
                  ""SupplementalQueries"": {
                    ""0"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Condition"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""ResourceIds"",
                          ""Name"": ""encounter"",
                          ""Resource"": ""Encounter"",
                          ""Paged"": ""100""
                        }
                      ]
                    },
                    ""1"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Coverage"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        }
                      ]
                    },
                    ""2"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Observation"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        },
                        {
                          ""ParameterType"": ""Literal"",
                          ""Name"": ""category"",
                          ""Literal"": ""imaging,laboratory,social-history,vital-signs""
                        }
                      ]
                    },
                    ""3"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""Procedure"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""4"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""ServiceRequest"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""ResourceIds"",
                          ""Name"": ""encounter"",
                          ""Resource"": ""Encounter"",
                          ""Paged"": ""100""
                        }
                      ]
                    },
                    ""5"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""DiagnosticReport"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""date"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        }
                      ]
                    },
                    ""6"": {
                      ""QueryConfigType"": ""Parameter"",
                      ""ResourceType"": ""MedicationRequest"",
                      ""Parameters"": [
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""patient"",
                          ""Variable"": 0,
                          ""Format"": null
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""authoredon"",
                          ""Variable"": 1,
                          ""Format"": ""ge{0}""
                        },
                        {
                          ""ParameterType"": ""Variable"",
                          ""Name"": ""authoredon"",
                          ""Variable"": 3,
                          ""Format"": ""le{0}""
                        },
                        {
                          ""ParameterType"": ""Literal"",
                          ""Name"": ""intent"",
                          ""Literal"": ""order""
                        }
                      ]
                    },
                    ""7"": {
                      ""QueryConfigType"": ""Reference"",
                      ""ResourceType"": ""Medication"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    },
                    ""8"": {
                      ""QueryConfigType"": ""Reference"",
                      ""ResourceType"": ""Specimen"",
                      ""OperationType"": 1,
                      ""Paged"": 100
                    },
                    ""9"": {
                      ""QueryConfigType"": ""Reference"",
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
                output.WriteLine("DISCHARGE Query Plan was successfully Created");
                return;
            }
            if (responseCodeString == "Conflict")
            {
                output.WriteLine("ALERT - Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc() - There is an existing DISCHARGE Query Plan for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Query Plan was NOT successfully created. Please reauthenticate. Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc() - FAILED");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  Query Plan was NOT successfully created. The Service is unavailable, please alert dev team. Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  DISCHARGE Query Plan was not successfully created. Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasureFHIRQueryListByFacility_AdHoc()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/data/fhirQueryList", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            var body = @"{
              ""facilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
              ""fhirBaseServerUrl"": """ + TestConfig.InternalFhirServerBase + @""",
              ""Authentication"": null,
              ""ehrPatientLists"": [
                {
                  ""Status"": ""Admit"",
                  ""TimeFrame"": ""LessThan24Hours"",
                  ""FhirId"": ""Stu3-AdHocSmokeTest-Admit-LessThan24Hrs""
                },
{
                  ""Status"": ""Admit"",
                  ""TimeFrame"": ""Between24To48Hours"",
                  ""FhirId"": ""Stu3-AdHocSmokeTest-Admit-Between24To48Hrs""
                },
{
                  ""Status"": ""Admit"",
                  ""TimeFrame"": ""MoreThan48Hours"",
                  ""FhirId"": ""Stu3-AdHocSmokeTest-Admit-MoreThan48Hrs""
                },
{
                  ""Status"": ""Discharge"",
                  ""TimeFrame"": ""LessThan24Hours"",
                  ""FhirId"": ""Stu3-AdHocSmokeTest-Discharge-LessThan24Hrs""
                },
{
                  ""Status"": ""Discharge"",
                  ""TimeFrame"": ""Between24To48Hours"",
                  ""FhirId"": ""Stu3-AdHocSmokeTest-Discharge-Between24To48Hrs""
                },
{
                  ""Status"": ""Discharge"",
                  ""TimeFrame"": ""MoreThan48Hours"",
                  ""FhirId"": ""Stu3-AdHocSmokeTest-Discharge-MoreThan48Hrs""
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
                output.WriteLine("Query List was successfully created");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                output.WriteLine("ALERT - Create_SingleMeasureFHIRQueryListByFacility_AdHoc() - There is an existing Query List for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  Query List was NOT successfully created. Please reauthenticate. Create_SingleMeasureFHIRQueryListByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  Query List was NOT successfully created. The Service is unavailable, please alert dev team. Create_SingleMeasureFHIRQueryListByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  Query List was not successfully created. Create_SingleMeasureFHIRQueryListByFacility_AdHoc() FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void Create_SingleMeasureFacilityNormalizationConfig_AdHoc()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/normalization/", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            var body = @"{
                ""FacilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
                ""OperationSequence"": {
                    ""0"": {
                        ""$type"": ""ConceptMapOperation"",
                        ""FacilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
                        ""name"": """ + TestConfig.SingleMeasureAdHocFacility + @""" Concept Map example"",
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
                        ""facilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
                        ""name"": ""PeriodDateFixer"",
                        ""conditions"": [],
                        ""transformResource"": """",
                        ""transformElement"": ""Period"",
                        ""transformValue"": """"
                    },
                    ""3"": {
                        ""$type"": ""ConditionalTransformationOperation"",
                        ""facilityId"": """ + TestConfig.SingleMeasureAdHocFacility + @""",
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
        }    //unused at the moment.
        public void GenerateSingleMeasureAdHocReport_ACH()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/facility/{TestConfig.SingleMeasureAdHocFacility}/AdhocReport", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            var body = @"{
                ""BypassSubmission"": false,
                ""StartDate"": ""2025-05-01T00:00:00Z"",
                ""EndDate"": ""2025-06-11T23:59:59.99Z"",
                ""ReportTypes"": [""" + TestConfig.MeasureAch + @"""],
                ""PatientIds"": [""x25sJU80vVa51mxJ6vSDcjbNC3BcdCQujJbXQwqdppFOO"", ""MVLkMLWErl3gQGRCuA2mygtVuix7PMBFBh9WVayaCL7xM"", ""CYUcGIlSrpJxCBMeEml30YSmE0Ea7loNBPVZfhCUkv7A3"", ""VsZkAG8h9vkGcL528ZcJxVXynyj8X39GaDfjHbA9AnvyA"", ""jjMZxCVWUbZgLkPf2LTzvZIBOW76YLJdIGCw8JFaTPiZg"", ""6tZ8Wt8maJdDFLvEsDcKmAaCAcSOxjr0mB8RjEi5Szw7H""]
            }";
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            string reportGuid = response.Content;
            JObject json = JObject.Parse(reportGuid);
            string reportIdGuid = (string)json["reportId"];
            TestConfig.TestContextStore.AdHocReportTrackingIdGuid = reportIdGuid;
            var responseCode = response.StatusCode;
            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK")
            {
                output.WriteLine("AdHoc Report was successfully scheduled");
                return;
            }
            if (responseCodeString == "Conflict" || responseCodeString == "BadRequest")
            {
                output.WriteLine("ALERT - GenerateSingleMeasureAdHocReport_ACH() - There is an existing AdHoc Report for this facility");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  AdHoc Report was NOT successfully scheduled. GenerateSingleMeasureAdHocReport_ACH() - Please reauthenticate.");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "ServiceUnavailable")
            {
                output.WriteLine("🔴  AdHoc Report was NOT successfully scheduled. GenerateSingleMeasureAdHocReport_ACH() - The Service is unavailable, please alert dev team.");
                Xunit.Assert.Fail();
            }
            else
            {
                output.WriteLine("🔴  AdHoc Report was not successfully configured. GenerateSingleMeasureAdHocReport_ACH() - FAILED");
                Xunit.Assert.Fail();
            }
        }
        public void GETSingleMeasureAdHocSubmissionDownloadReport()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };

            var client = new RestClient(options);
            var request = new RestRequest($"/Submission/{TestConfig.SingleMeasureAdHocFacility}/{AdHocReportGuid}?external=true", Method.Get);
            RestResponse response = client.ExecuteAsync(request).GetAwaiter().GetResult();
            WaitForRequestComplete();
            JObject jsonResponse = JObject.Parse(response.Content);
            var responseCode = response.StatusCode;
            string responseCodeString = responseCode.ToString();
            if (responseCodeString == "OK" || responseCodeString == "Created")
            {
                output.WriteLine("AdHoc report was successfully downloaded to viewer.");
                return;
            }
            if (responseCodeString == "Unauthorized")
            {
                output.WriteLine("🔴  AdHoc report was NOT downloaded. GETSingleMeasureAdHocSubmissionDownloadReport() - Check to make sure you are properly authenticated.");
                Xunit.Assert.Fail();
            }
            if (responseCodeString == "BadRequest")
            {
                output.WriteLine("🔴  AdHoc report was NOT downloaded. GETSingleMeasureAdHocSubmissionDownloadReport() - Please check the GETSubmissionDownloadReport request");
                Xunit.Assert.Fail();
            }
        }
        public void GETSingleMeasureAdHocFacilityValidationResultsForReport()
        {
            WaitForRequestComplete();
            var options = new RestClientOptions(TestConfig.AdminBffBase)
            {
                MaxTimeout = -1,
            };

            var client = new RestClient(options);
            var request = new RestRequest($"/validation/result/{TestConfig.SingleMeasureAdHocFacility}/{AdHocReportGuid}", Method.Get);
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
                output.WriteLine("[ERROR] The Get Validation Report request was NOT successful. GETSingleMeasureAdHocFacilityValidationResultsForReport() - Authentication failed.");
                Xunit.Assert.Fail("Unauthorized request.");
            }
            if (responseCodeString == "BadRequest")
            {
                output.WriteLine("[ERROR] The Get Validation Report request was NOT successful. GETSingleMeasureAdHocFacilityValidationResultsForReport() - Please verify the request parameters.");
                Xunit.Assert.Fail("Bad request.");
            }
            output.WriteLine($"[ERROR] Unexpected response: {responseCodeString}");
            Xunit.Assert.Fail($"Unexpected validation report response: {responseCodeString}");
        }
        #endregion
    }
}
