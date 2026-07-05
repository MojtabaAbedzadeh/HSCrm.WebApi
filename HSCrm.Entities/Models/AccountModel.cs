using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Models
{
    public class UserLoginModel
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required.")]
        public required string UserName { get; set; }
        [Required(AllowEmptyStrings = false, ErrorMessage = "Password is required.")]
        public required string Password { get; set; }
        public long FiscalYearId { get; set; }
    }

    public class LoginModel
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required.")]
        public required string UserName { get; set; }
        [Required(AllowEmptyStrings = false, ErrorMessage = "Password is required.")]
        public required string Password { get; set; }
    }

    public class UserJwtToken
    {
        public string UserId { get; set; }
        public string TenantId { get; set; }
        public string Token { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserImageUrl { get; set; }
        public string FiscalYear { get; set; }
        public byte FiscalYearStatus { get; set; }
        public bool UserStatus { get; set; }
        public byte UserType { get; set; }
        public IList<string>? Roles { get; set; }
        public IList<string>? Permissions { get; set; }
    }
}
