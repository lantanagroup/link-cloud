using Hl7.Fhir.Utility;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public class ValidateEmailAddressCommand : IValidateEmailAddressCommand
    {
        public Task<bool> Execute(string emailAddress, CancellationToken cancellationToken = default)
        {
            string regex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

            var valid = Regex.IsMatch(emailAddress, regex, RegexOptions.IgnoreCase);

            return Task.FromResult(valid);
        }
    }
}
