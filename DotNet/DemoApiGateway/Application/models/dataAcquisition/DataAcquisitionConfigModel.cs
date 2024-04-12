namespace LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition
{
    public class DataAcquisitionConfigModel
    {
        public string TenantId { get; set; }
        public List<Facility> Facilities { get; set; }
        public DataAcquisitionConfigModel() { }
    }

    public class Facility
    {
        public string FacilityId { get; set; }
        public string FhirVersion { get; set; }
        public string BaseFhirUrl { get; set; }
        public AuthenticationConfiguration Auth { get; set; }
        public List<ConfigResource> ResourceSettings { get; set; }
        public Facility() { }
    }

    //public class AuthenticationConfiguration
    //{
    //    public string AuthType { get; set; }
    //    public string Key { get; set; }
    //    public string TokenUrl { get; set; }
    //    public string Audience { get; set; }
    //    public string ClientId { get; set; }
    //    public string UserName { get; set; }
    //    public string Password { get; set; }
    //    public AuthenticationConfiguration() { }
    //}

    public class ConfigResource
    {
        public List<string> ResourceType { get; set; }
        public string ConfigType { get; set; }
        public bool IsBulk { get; set; }
        public UsCoreResource UsCore { get; set; }
        public bool UseBaseAuth { get; set; }
        public AuthenticationConfiguration Auth { get; set; }
        public string LastUpdatedBy { get; set; }
        public string LastUpdatedDateTime { get; set; }
        public ConfigResource() { }
    }

    public class UsCoreResource
    {
        public bool UseBaseFhirEndpoint { get; set; }
        public string BaseFhirUrl { get; set; }
        public bool UseDefaultRelativeFhirPath { get; set; }    
        public string RelativeFhirPath { get; set; }
        public bool UseDefaultParameters { get; set; }    
        public List<OverrideTenantParameters> Parameters { get; set; }
    }

    public class OverrideTenantParameters
    {
        public string Name { get; set; }
        public List<string> Values { get; set; }
        public bool IsQuery { get; set; }
        public OverrideTenantParameters() { }
    }

    public enum QueryConfigurationTypePathParameter
    {
        fhirQueryConfiguration, fhirQueryListConfiguration
    }

}
