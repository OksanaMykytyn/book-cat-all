namespace BookCatAPI.Models.DTOs
{
    public class CreateDocumentDto
    {
        public string Name { get; set; }
        public string Format { get; set; } // writeOffAct або inventoryBook
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

}
