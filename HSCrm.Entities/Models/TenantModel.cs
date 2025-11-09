namespace HSCrm.Entities.Models
{
    public class BusinessCreateModel
    {
        public required string BusinessTitle { get; set; }
        public required string BusinessOwner { get; set; }
        public required string BusinessOwnerMobile { get; set; }
    }

    public class BusinessEditModel
    {
        public int BusinessId { get; set; }
        public required string BusinessTitle { get; set; }
        public required string BusinessOwner { get; set; }
        public required string BusinessOwnerMobile { get; set; }
    }
}
