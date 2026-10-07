using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Models
{
    public class UserCreateModel
    {
        public required string UserFirstNameC { get; set; }
        public required string UserLastNameC { get; set; }
        public required string UserNameC { get; set; }
        public required string PhoneNumberC { get; set; }
        public string? EmailC { get; set; }
        public string? UserImageUrlC { get; set; }       
        public bool UserGenderC { get; set; }
        public int TenantId { get; set; }
    }
    public class UserModelDto
    {
        public string Id { get; set; }
        public string UserFirstName { get; set; }
        public string UserLastName { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string UserImageUrl { get; set; }
        public bool UserStatus { get; set; }
        public string RoleName { get; set; }
    }
    public class UserEditModel
    {
        public required string UserId { get; set; }
        public required string UserFirstName { get; set; }
        public required string UserLastName { get; set; }
        public required string PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? UserImageUrl { get; set; }
        public bool UserGender { get; set; }
    }

    public class AssignRoleModel
    {
        public required string UserId { get; set; }
        public required string RoleId { get; set; }
    }
}
