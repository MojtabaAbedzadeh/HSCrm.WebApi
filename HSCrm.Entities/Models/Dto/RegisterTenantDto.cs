namespace HSCrm.Entities.Models.Dto
{
    public class RegisterTenantDto
    {
        public string CompanyName { get; set; } = default!;
        public string CompanyPhone { get; set; } = default!;
        public string CompanyEmail { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string UserName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;
        public string Password { get; set; } = default!;
    }
}