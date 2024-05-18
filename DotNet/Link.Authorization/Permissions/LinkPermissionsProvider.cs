namespace Link.Authorization.Permissions
{
    public static class LinkPermissionsProvider
    {
        public static List<LinkSystemPermissions> GetLinkPermissions()
        {
            return Enum.GetValues(typeof(LinkSystemPermissions)).OfType<LinkSystemPermissions>().ToList();
        }
    }
}
