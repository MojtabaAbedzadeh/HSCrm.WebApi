using Microsoft.AspNetCore.Identity;

namespace HSCrm.Entities.Entities
{
    public class ApplicationUser : IdentityUser<string>
    {
        public required string UserFirstName { get; set; }
        public required string UserLastName { get; set; }
        public string? UserImageUrl { get; set; }
        public bool UserStatus { get; set; }
        public byte UserType { get; set; }
        public bool UserGender { get; set; }
    }
}
