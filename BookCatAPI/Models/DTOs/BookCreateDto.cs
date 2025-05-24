namespace BookCatAPI.Models.DTOs
{
    public class BookCreateDto
    {
        public string? InventoryNumber { get; set; }
        public string Name { get; set; }
        public string? Author { get; set; }
        public string? Udk { get; set; }
        public string? UdkFormDocument { get; set; }
        public decimal? Price { get; set; }
        public string? CheckDocument { get; set; }
        public int? YearPublishing { get; set; }
        public DateTime? Removed { get; set; } = null;
    }
}
