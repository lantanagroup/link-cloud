BEGIN TRANSACTION

DECLARE @FacilityId NVARCHAR(MAX) 
SET @FacilityId = 'Test-Hospital'

USE [link-tenant]

INSERT INTO [dbo].[Facilities]
           ([Id]
           ,[FacilityId]
           ,[FacilityName]
           ,[MRPModifyDate]
           ,[MRPCreatedDate]
           ,[CreateDate]
           ,[ModifyDate]
           ,[MonthlyReportingPlans]
           ,[ScheduledTasks])
     VALUES
           (
			NEWID()
           ,@FacilityId
           ,@FacilityId
           ,NULL
           ,GETDATE()
           ,GETDATE()
           ,NULL
           ,'[]'
           ,'[{"KafkaTopic":"ReportScheduled","ReportTypeSchedules":[{"ReportType":"NHSNdQMAcuteCareHospitalInitialPopulation","ScheduledTriggers":"[\u00220 0 0/2 * * ?\u0022]"}]}]')

PRINT('Tenant facility created')


USE [link-census]

INSERT INTO [dbo].[CensusConfig]
           ([Id]
           ,[FacilityID]
           ,[ScheduledTrigger]
           ,[CreateDate]
           ,[ModifyDate])
     VALUES
           (NEWID()
           ,@FacilityId
           ,'0 0 0/3 * * ?'
           ,GETDATE()
           ,GETDATE())

PRINT('Census config created')

USE [link-querydispatch]

INSERT INTO [dbo].[queryDispatchConfigurations]
           ([Id]
           ,[DispatchSchedules]
           ,[FacilityId]
           ,[CreateDate]
           ,[ModifyDate])
     VALUES
           (NEWID()
           ,'[{"Event":0,"Duration":"PT10S"}]'
           ,@FacilityId
           ,GETDATE()
           ,GETDATE())

PRINT('Query Dispatch config created')

USE [link-dataacquisition]

INSERT INTO [dbo].[fhirQueryConfiguration]
           ([Id]
           ,[FacilityId]
           ,[FhirServerBaseUrl]
           ,[Authentication]
           ,[QueryPlanIds]
           ,[CreateDate]
           ,[ModifyDate])
     VALUES
           (NEWID()
           ,@FacilityId
           ,'** INSERT FHIR BASE URL HERE **'
           ,'{}'
           ,'["NHSNdQMAcuteCareHospitalInitialPopulation"]'
           ,GETDATE()
           ,GETDATE())

INSERT INTO [dbo].[fhirListConfiguration]
           ([Id]
           ,[FacilityId]
           ,[FhirBaseServerUrl]
           ,[Authentication]
           ,[EHRPatientLists]
           ,[CreateDate]
           ,[ModifyDate])
     VALUES
           (NEWID()
           ,@FacilityId
           ,'** INSERT FHIR BASE URL HERE **'
           ,NULL
           ,'[{"ListIds":["** INSERT LIST ID HERE **"],"MeasureIds":null}]'
           ,GETDATE()
           ,GETDATE())

PRINT('Data Acquisition config created')

USE [link-normalization]

INSERT INTO [dbo].[NormalizationConfig]
           ([FacilityId]
           ,[OperationSequence]
           ,[ModifyDate]
           ,[Id]
           ,[CreateDate])
     VALUES
           (@FacilityId
           ,'{"0":{"$type":"ConceptMapOperation","FacilityId":"superfacility","Name":null,"FhirConceptMap":{"resourceType":"ConceptMap","id":"ehr-test-epic-encounter-class","url":"https://nhsnlink.org/fhir/ConceptMap/ehr-test-epic-encounter-class","identifier":{"system":"urn:ietf:rfc:3986","value":"urn:uuid:63cd62ee-033e-414c-9f58-3ca97b5ffc3b"},"version":"20220728","name":"ehr-test-epic-encounter-class","title":"Ehr-test Epic Encounter Class ConceptMap","status":"draft","experimental":true,"date":"2022-07-28","description":"A mapping between the Epic\u0027s Encounter class codes and HL7 v3-ActEncounter codes","purpose":"To help implementers map from University of Michigan Epic to FHIR","group":[{"source":"urn:oid:1.2.840.114350.1.72.1.7.7.10.696784.13260","target":"http://terminology.hl7.org/CodeSystem/v3-ActCode","element":[{"code":"1","target":[{"code":"IMP","display":"inpatient","equivalence":"inexact"}]},{"code":"2","target":[{"code":"IMP","display":"inpatient","equivalence":"inexact"}]},{"code":"3","target":[{"code":"IMP","display":"inpatient","equivalence":"inexact"}]},{"code":"4","target":[{"code":"IMP","display":"inpatient","equivalence":"inexact"}]},{"code":"5","target":[{"code":"IMP","display":"inpatient","equivalence":"inexact"}]},{"code":"6","target":[{"code":"IMP","display":"inpatient","equivalence":"inexact"}]}]}]},"FhirPath":null,"FhirContext":"Encounter","CreateDate":"0001-01-01T00:00:00","ModifiedDate":"0001-01-01T00:00:00"},"1":{"$type":"CopyLocationIdentifierToTypeOperation","Name":"Test Location Type"},"2":{"$type":"ConditionalTransformationOperation","FacilityId":"superfacility","Name":"PeriodDateFixer","Conditions":[],"TransformResource":"","TransformElement":"Period","TransformValue":"","CreateDate":"0001-01-01T00:00:00","ModifiedDate":"0001-01-01T00:00:00"},"3":{"$type":"ConditionalTransformationOperation","FacilityId":"superfacility","Name":"EncounterStatusTransformation","Conditions":[],"TransformResource":"Encounter","TransformElement":"Status","TransformValue":"","CreateDate":"0001-01-01T00:00:00","ModifiedDate":"0001-01-01T00:00:00"}}'
           ,NULL
           ,NEWID()
           ,GETDATE())

PRINT('Normalization config created')

--ROLLBACK TRANSACTION
COMMIT TRANSACTION