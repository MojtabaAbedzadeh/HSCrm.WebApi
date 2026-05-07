using Microsoft.AspNetCore.Identity;

namespace HSCrm.Entities.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public required string UserFirstName { get; set; }
        public required string UserLastName { get; set; }
        public string? UserImageUrl { get; set; }
        public bool UserStatus { get; set; }
        /// <summary>
        /// 0 -> User
        /// 1 -> Admin
        /// /// 3 -> Customer
        /// </summary>
        public byte UserType { get; set; }
        public bool UserGender { get; set; }
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;
        public bool IsTenantOwner { get; set; }
    }
}
