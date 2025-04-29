using FluentValidation;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Application.Validators
{
    public class UpdateDataAcquisitionLogModelValidator : AbstractValidator<UpdateDataAcquisitionLogModel>
    {
        public UpdateDataAcquisitionLogModelValidator()
        {
            RuleFor(x => x.ScheduledExecutionDate)
                .Must(date => date == null || date > DateTime.UtcNow)
                .WithMessage("Scheduled execution date cannot be in the past if the current status is pending.")
                .When(x => x.Status == RequestStatusModel.Pending);

            RuleFor(x => x.ScheduledExecutionDate)
                .Must(date => date == null || date > DateTime.MinValue)
                .WithMessage("Scheduled execution date must be a valid date.");
        }
    }
}
