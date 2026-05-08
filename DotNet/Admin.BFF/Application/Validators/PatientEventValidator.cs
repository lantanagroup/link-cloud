using FluentValidation;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Validation
{
    public class PatientEventValidator : AbstractValidator<PatientEvent>
    {
        public PatientEventValidator()
        {
            RuleFor(x => x.Key)
                .NotEmpty()
                    .WithMessage("Key is required");

            RuleFor(x => x.PatientId)
                .NotEmpty()
                    .WithMessage("PatientId is required");

            RuleFor(x => x.EventType)
                .NotEmpty()
                    .WithMessage("EventType is required")
                .Must(x => x == "Admission" || x == "Discharge")
                    .WithMessage("EventType must be 'Admission' or 'Discharge'");
        }
    }
}
