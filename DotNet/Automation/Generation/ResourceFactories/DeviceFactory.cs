using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class DeviceFactory
{
    /// <summary>Generate a Device from the shared pool using a seed index.</summary>
    public static Device Generate(string id, int seed, string? patientId = null)
    {
        var variants = new[]
        {
            ("706689003", "Pulse oximeter"),
            ("706172005", "Ventilator"),
            ("10776007",  "Continuous positive airway pressure device"),
        };
        var (snomedCode, display) = variants[seed % variants.Length];
        return Create(id, snomedCode, display, patientId);
    }

    /// <summary>Create a Device with caller-supplied SNOMED type code and display.</summary>
    public static Device Create(string id, string snomedCode, string display, string? patientId)
    {
        var device = new Device
        {
            Id = id,
            Status = Device.FHIRDeviceStatus.Active,
            Type = Snomed(snomedCode, display),
            DeviceName =
            [
                new Device.DeviceNameComponent { Name = display, Type = DeviceNameType.UserFriendlyName }
            ],
            Manufacturer = "SyntheticMed Devices Inc.",
            SerialNumber = $"SN-{id.GetStableHash32():X8}"
        };
        if (patientId != null)
            device.Patient = Ref($"Patient/{patientId}");
        return device;
    }
}
