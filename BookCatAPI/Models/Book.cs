using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BookCatAPI.Models
{
    public class Book
    {
        public int Id { get; set; }
        [Column("inventory_number")]
        public string? InventoryNumber { get; set; }
        public string Name { get; set; }
        public string? Author { get; set; }
        public string? Udk { get; set; }
        [Column("udk_form_document")]
        public string? UdkFormDocument { get; set; }
        [Precision(10, 2)]
        public decimal? Price { get; set; }
        [Column("check_document")]
        public string? CheckDocument { get; set; }
        [Column("year_publishing")]
        public int? YearPublishing { get; set; }
        [Column("library_id")]
        public int? LibraryId { get; set; }
        public DateTime? Removed { get; set; }
        [Column("create_at")]
        public DateTime CreateAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public Library Library { get; set; }
    }

}
