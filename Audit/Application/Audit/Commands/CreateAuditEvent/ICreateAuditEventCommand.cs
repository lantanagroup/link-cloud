namespace LantanaGroup.Link.Audit.Application.Commands
{
    public interface ICreateAuditEventCommand
    {
        Task<string> Execute(CreateAuditEventModel model);
    }
}
