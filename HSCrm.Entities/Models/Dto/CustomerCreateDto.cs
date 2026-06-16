namespace HSCrm.Entities.Models.Dto
{
    public class CustomerCreateDto
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Phone { get; set; }

        public string Email { get; set; }

        public string Address { get; set; }

        public int TenantId { get; set; }

        public string ApplicationUserId { get; set; }
    }

    public class CustomerCreateModel
    {
        public string UserFirstName { get; set; }
        public string UserLastName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string CreateById { get; set; }
        public int TenantId { get; set; }
        public string? UserImageUrlC { get; set; }
    }
}