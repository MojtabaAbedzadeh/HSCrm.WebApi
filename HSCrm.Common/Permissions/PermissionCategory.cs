namespace HSCrm.Common.Permissions
{
    public class PermissionCategory
    {
        public string Key { get; set; } = null!;
        public string TitleFa { get; set; } = null!;
        public string Icon { get; set; } = null!;
        public int Order { get; set; }
    }
}