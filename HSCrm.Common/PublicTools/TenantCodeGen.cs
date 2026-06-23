namespace HSCrm.Common.PublicTools
{
    public static class TenantCodeGenerator
    {
        public static string Generate(string companyName)
        {
            var slug = SlugHelper.Slugify(companyName);

            var random = Guid.NewGuid().ToString("N").Substring(0, 4);

            return $"{slug}-{random}";
        }
    }
}