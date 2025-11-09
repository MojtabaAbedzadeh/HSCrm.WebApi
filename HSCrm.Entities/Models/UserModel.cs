using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Models
{
    public class UserCreateModel
    {
        public int Id { get; set; }
        public required string UserFirstName { get; set; }
        public required string UserLastName { get; set; }
        public required string UserName { get; set; }
        public required string PhoneNumber { get; set; }
        public string? UserImageUrl { get; set; }
        public bool UserStatus { get; set; }
        public byte UserType { get; set; }
        public bool UserGender { get; set; }
    }

    public class UserEditModel
    {
        public required string UserId { get; set; }
        public required string UserFirstName { get; set; }
        public required string UserLastName { get; set; }
        public required string UserName { get; set; }
        public required string PhoneNumber { get; set; }
        public string? UserImageUrl { get; set; }
        public bool UserStatus { get; set; }
        public byte UserType { get; set; }
        public bool UserGender { get; set; }
    }
}
