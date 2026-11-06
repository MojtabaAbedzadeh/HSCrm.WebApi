namespace HSCrm.Common.Permissions
{
    /// <summary>
    /// Platform permissions are intentionally limited to operations that span tenants.
    /// All unclassified categories default to tenant scope.
    /// </summary>
    public static class PermissionScope
    {
        public const string PlatformCategory = "Tenants";

        public static bool IsPlatformPermission(string? categoryTitleEn) =>
            string.Equals(categoryTitleEn, PlatformCategory, StringComparison.OrdinalIgnoreCase);
    }
}