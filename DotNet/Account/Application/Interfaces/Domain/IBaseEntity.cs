namespace LantanaGroup.Link.Account.Application.Interfaces.Domain
{
    public interface IBaseEntity
    {
        DateTime CreatedOn { get; set; }
        string? CreatedBy { get; set; }
        DateTime? LastModifiedOn { get; set; }
        string? LastModifiedBy { get; set; }        
    }
}
