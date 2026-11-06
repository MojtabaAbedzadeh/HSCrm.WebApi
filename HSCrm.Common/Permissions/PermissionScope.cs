namespace Common.Permissions
{
    /// <summary>
    /// Defines which permission categories may be assigned to tenant roles.
    /// TenantId 1 is the central platform tenant; other tenants cannot administer tenants.
    /// </summary>
    public static class PermissionScope
    {
        public const int PlatformTenantId = 1;
        public const string PlatformTenantCategory = "Tenants";

        public static bool IsAssignableToTenant(string categoryTitleEn, int tenantId)
        {
            return tenantId == PlatformTenantId ||
                   !string.Equals(categoryTitleEn, PlatformTenantCategory, StringComparison.OrdinalIgnoreCase);
        }
    }
}