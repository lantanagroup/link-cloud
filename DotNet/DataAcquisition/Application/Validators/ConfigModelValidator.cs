using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Entities;

namespace LantanaGroup.Link.DataAcquisition.Application.Validators;

public class ConfigModelValidator
{
    //private readonly TenantDataAcquisitionConfigModel _configModel;

    public ConfigModelValidator()
    {
        //_configModel = configModel ?? throw new ArgumentNullException(nameof(configModel));
    }

    public ValidationResults ValidateConfigModel(TenantDataAcquisitionConfigModel configModel)
    {
        var results = new ValidationResults
        {
            IsSuccess = true,
            ErrorMessages = new List<string>()
        };

        if (configModel is null)
        {
            results.IsSuccess = false;
            results.ErrorMessages.Add(DataAcquisitionConstants.ValidationErrorMessages.NullConfigModel);
        }

        if (configModel!.TenantId is null)
        {
            results.IsSuccess = false;
            results.ErrorMessages.Add(DataAcquisitionConstants.ValidationErrorMessages.NullTenantId);
        }

        if (configModel!.Facilities is null)
        {
            results.IsSuccess = false;
            results.ErrorMessages.Add(DataAcquisitionConstants.ValidationErrorMessages.NullFacilities);
        }

        if (configModel!.Facilities!.Count < 1)
        {
            results.IsSuccess = false;
            results.ErrorMessages.Add(DataAcquisitionConstants.ValidationErrorMessages.NoFacilities);
        }

        return results;
    }

    public ValidationResults ValidateFacility(TenantDataAcquisitionConfigModel configModel, string facilityId)
    {
        var results = new ValidationResults();
        var facility = configModel.Facilities.SingleOrDefault(x => x.FacilityId == facilityId);

        if (facility == null)
        {
            results.IsSuccess = false;
            results.ErrorMessages.Add(DataAcquisitionConstants.ValidationErrorMessages.NullFacilities);
        }

        if (facility?.FacilityId is null)
        {
            results.IsSuccess = false;
            results.ErrorMessages.Add(DataAcquisitionConstants.ValidationErrorMessages.NullFacilityId);
        }

        if (facility?.ResourceSettings is null)
        {
            results.IsSuccess = false;
            results.ErrorMessages.Add(DataAcquisitionConstants.ValidationErrorMessages.NullResourceSettings);
        }

        if (facility?.ResourceSettings.Count == 0)
        {
            results.IsSuccess = false;
            results.ErrorMessages.Add(DataAcquisitionConstants.ValidationErrorMessages.NoResourceSettings);
        }

        return results;
    }
}
