namespace HSCrm.Entities.Models
{
    public class CustomerCreateModel
    {
        public required string UserFirstName { get; set; }
        public required string UserLastName { get; set; }
        public required string UserName { get; set; }
        public required string Password { get; set; }
        public bool UserGender { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }

    public class CustomerEditModel
    {
        public int CustomerId { get; set; }
        public required string Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? ModifiedById { get; set; }
        public int TenantId { get; set; }
    }

    public class CustomerDetailViewModel
    {
        public int Id { get; set; }
        public required string UserFirstName { get; set; }
        public required string UserLastName { get; set; }
        public required string UserName { get; set; }
        public string? Phone { get; set; }
        public string?  Email { get; set; }
        public string? Address { get; set; }        
        public required string UserId { get; set; }
        public string? UserImageUrl { get; set; }        
        public bool UserStatus { get; set; }        
        public byte UserType { get; set; }        
        public bool UserGender { get; set; }        
        public int TenantId { get; set; }        
        public bool IsTenantOwner { get; set; }
    }
}
