using System.ComponentModel.DataAnnotations.Schema;

namespace BookCatAPI.Models
{
    public class Document
    {
        public int Id { get; set; }
        public int? LibraryId { get; set; }
        public string? Format { get; set; }
        public DateTime? DateStart { get; set; }
        public DateTime? DateEnd { get; set; }
        public string? Url { get; set; }
        public string? Name { get; set; }
        [Column("create_at")]
        public DateTime CreateAt { get; set; } = DateTime.Now;

        public Library Library { get; set; }
    }

}
