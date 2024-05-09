﻿using Hl7.Fhir.Utility;

namespace LantanaGroup.Link.Submission.Settings
{
    public static class SubmissionConstants
    {

        public const string ServiceName = "Submission";

        public static class AppSettingsSectionNames
        {
            public const string Mongo = "MongoDB";
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
            public const string Serilog = "Serilog";
        }

    }
}
