using Microsoft.AspNetCore.Identity;

namespace HSCrm.Entities.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string UserFirstName { get; set; }
        public string UserLastName { get; set; }
        public string? UserImageUrl { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;

        public bool IsTenantOwner { get; set; }
        public bool UserStatus { get; set; }

        /// <summary>
        /// 110 -> Super Admin
        /// 0 -> User
        /// 1 -> Admin
        /// 3 -> Customer
        /// 4 -> Owner
        /// </summary>
        public byte UserType { get; set; }
        public bool UserGender { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
