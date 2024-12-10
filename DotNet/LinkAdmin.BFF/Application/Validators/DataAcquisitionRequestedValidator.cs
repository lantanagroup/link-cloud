using FluentValidation;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Validators
{
    public class DataAcquisitionRequestedValidator : AbstractValidator<DataAcquisitionRequested>
    {
        public DataAcquisitionRequestedValidator()
        {
            RuleFor(x => x.Key)
                .NotEmpty()
                    .WithMessage("Key is required");

            RuleFor(x => x.PatientId)
                .NotEmpty()
                    .WithMessage("PatientId is required");

            RuleFor(x => x.QueryType)
                .NotEmpty()
                    .WithMessage("QueryType is required")
                .Must((x, y) => x.QueryType == "Initial" || x.QueryType == "Supplemental")
                    .WithMessage("QueryType must be Initial or Supplemental");

            RuleFor(x => x.ScheduledReports)
                .NotEmpty()
                    .WithMessage("Reports are required");

            RuleForEach(x => x.ScheduledReports)
                .SetValidator(new ScheduledReportValidator());

        }
    }

    public class ScheduledReportValidator : AbstractValidator<ScheduledReport>
    {
        public ScheduledReportValidator()
        {
            RuleFor(x => x.ReportType)
                .NotEmpty()
                    .WithMessage("ReportType is required");

            RuleFor(x => x.StartDate)
                .NotEmpty()
                    .WithMessage("StartDate is required")
                .Must((x, y) => x.StartDate < x.EndDate)
                    .WithMessage("StartDate must be before EndDate")
                .Must((x, y) => x.StartDate < DateTime.Now)
                    .WithMessage("StartDate must be in the past");

            RuleFor(x => x.EndDate)
                .NotEmpty()
                    .WithMessage("EndDate is required");

        }

    }
}
