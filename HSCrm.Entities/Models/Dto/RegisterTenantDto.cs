using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Models.Dto
{
    public class RegisterTenantDto
    {
        [Required(ErrorMessage = "نام شرکت الزامی است.")]
        [StringLength(200, MinimumLength = 2)]
        public string CompanyName { get; set; } = default!;

        [Required(ErrorMessage = "تلفن شرکت الزامی است.")]
        [Phone]
        [StringLength(20)]
        public string CompanyPhone { get; set; } = default!;

        [Required(ErrorMessage = "ایمیل شرکت الزامی است.")]
        [EmailAddress]
        [StringLength(256)]
        public string CompanyEmail { get; set; } = default!;

        [Required(ErrorMessage = "نام الزامی است.")]
        [StringLength(100, MinimumLength = 1)]
        public string FirstName { get; set; } = default!;

        [Required(ErrorMessage = "نام خانوادگی الزامی است.")]
        [StringLength(100, MinimumLength = 1)]
        public string LastName { get; set; } = default!;

        [Required(ErrorMessage = "نام کاربری الزامی است.")]
        [StringLength(100, MinimumLength = 3)]
        public string UserName { get; set; } = default!;

        [Required(ErrorMessage = "ایمیل الزامی است.")]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "شماره موبایل الزامی است.")]
        [Phone]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = default!;

        [Required(ErrorMessage = "رمز عبور الزامی است.")]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = default!;
    }
}
