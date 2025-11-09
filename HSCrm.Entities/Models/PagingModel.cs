namespace HSCrm.Entities.Models
{
    public class PagingModel
    {
        public required string draw { get; set; }
        public string start { get; set; }
        public int filterId { get; set; }
        public string length { get; set; }
        public string? searchValue { get; set; }
        public string? sortColumn { get; set; }
        public string? sortColumnDirection { get; set; }
    }
}
