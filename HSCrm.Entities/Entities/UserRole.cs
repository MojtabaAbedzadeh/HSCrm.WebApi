using Microsoft.AspNetCore.Identity;
using System.Data;

namespace HSCrm.Entities.Entities
{
    public class UserRole : IdentityUserRole<string>
    {
        public ApplicationUser User { get; set; } = null!;
        public ApplicationRole Role { get; set; } = null!;
    }
}
