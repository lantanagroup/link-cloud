using FluentValidation;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Validators
{
    public class UpdateDataAcquisitionLogModelValidator : AbstractValidator<UpdateDataAcquisitionLogModel>
    {
        public UpdateDataAcquisitionLogModelValidator()
        {
            RuleFor(x => x.ExecutionDate)
                .Must(date => date == null || date > DateTime.UtcNow)
                .WithMessage("Scheduled execution date cannot be in the past if the current status is pending.")
                .When(x => x.Status == RequestStatus.Pending);

            RuleFor(x => x.ExecutionDate)
                .Must(date => date == null || date > DateTime.MinValue)
                .WithMessage("Scheduled execution date must be a valid date.");
        }
    }
}
