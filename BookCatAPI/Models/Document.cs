using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BookCatAPI.Models
{
    public class Document
    {
        public int Id { get; set; }
        [Column("library_id")]
        public int? LibraryId { get; set; }
        public string? Format { get; set; }
        [Column("date_start")]
        public DateTime? DateStart { get; set; }
        [Column("date_end")]
        public DateTime? DateEnd { get; set; }
        public string? Url { get; set; }
        public string? Name { get; set; }
        [Column("create_at")]
        public DateTime CreateAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public Library Library { get; set; }
    }

}
