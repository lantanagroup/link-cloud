using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public interface ICreateReportScheduled
    {
        Task<string> Execute(ReportScheduled model, string? userId = null);
    }
}
