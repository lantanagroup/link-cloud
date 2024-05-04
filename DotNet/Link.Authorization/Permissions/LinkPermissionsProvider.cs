namespace Link.Authorization.Permissions
{
    public static class LinkPermissionsProvider
    {
        public static List<LinkPermissions> GetLinkPermissions()
        {
            return Enum.GetValues(typeof(LinkPermissions)).OfType<LinkPermissions>().ToList();
        }
    }
}
