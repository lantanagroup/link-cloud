namespace LantanaGroup.Link.Account.Domain.Enums
{
    [Flags]
    public enum PermissionTypes
	{
		None = 0,
		Read = 1,
		Write = 2,
		ReadWrite = Read | Write
	}
}
