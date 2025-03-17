using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter;
using LantanaGroup.Link.Shared.Application.Models;
using System.Globalization;
using System.Xml;

namespace LantanaGroup.Link.DataAcquisition.Application.Factories.ParameterFactories;

public class VariableParameterFactory
{
    public static ParameterFactoryResult Build(VariableParameter parameter, GetPatientDataRequest request, ScheduledReport scheduledReport, string lookback)
    {
        ParameterFactoryResult parameterFactoryResult = parameter.Variable switch
        {
            Variable.LookbackStart => new ParameterFactoryResult(parameter.Name, CalculateLookBackStartDate(parameter, scheduledReport, lookback)),
            Variable.PatientId => new ParameterFactoryResult(parameter.Name, TEMPORARYPatientIdPart(request.ConsumeResult.Message.Value.PatientId)),
            Variable.PeriodStart => new ParameterFactoryResult(parameter.Name, ConvertDateTimeStringToUTCFormat(parameter, scheduledReport.StartDate.ToString("yyyy-MM-dd"), nameof(scheduledReport.StartDate))),
            Variable.PeriodEnd => new ParameterFactoryResult(parameter.Name, ConvertDateTimeStringToUTCFormat(parameter, scheduledReport.EndDate.ToString("yyyy-MM-dd"), nameof(scheduledReport.EndDate))),
            _ => throw new Exception("Invalid or null Variable type provided."),
        };

        if (string.IsNullOrWhiteSpace(parameterFactoryResult.key)
            || (string.IsNullOrWhiteSpace(parameterFactoryResult.value)
            || (parameterFactoryResult.paged && parameterFactoryResult.values?.Count == 0)))
            return null;

        return parameterFactoryResult;
    }

    private static string CalculateLookBackStartDate(VariableParameter parameter, ScheduledReport scheduledReport, string lookback)
    {
        TimeSpan ts = XmlConvert.ToTimeSpan(lookback);
        var date = scheduledReport.StartDate;

        date.Subtract(ts);

        var dateOnly = date.Date.ToString("yyyy-MM-dd");
        dateOnly = $"{dateOnly}T00:00:00Z";

        return string.Format(parameter.Format, dateOnly);
    }

    private static string ConvertDateTimeStringToUTCFormat(VariableParameter parameter, string dateTimeString, string field)
    {
        var success = DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date);

        if (!success)
        {
            throw new Exception($"Unable to parse {field}");
        }
        var dateOnly = date.Date.ToString("yyyy-MM-dd");

        if(parameter.Variable == Variable.PeriodStart)
            dateOnly = $"{dateOnly}T00:00:00Z";

        if(parameter.Variable == Variable.PeriodEnd)
            dateOnly = $"{dateOnly}T23:59:59Z";

        return string.Format(parameter.Format, dateOnly);
    }

    private static string TEMPORARYPatientIdPart(string fullPatientUrl)
    {
        var separatedPatientUrl = fullPatientUrl.Split('/');
        var patientIdPart = string.Join("/", separatedPatientUrl.Skip(Math.Max(0, separatedPatientUrl.Length - 2)));
        return patientIdPart;
    }

}
