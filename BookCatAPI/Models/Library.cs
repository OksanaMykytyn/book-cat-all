using System.ComponentModel.DataAnnotations.Schema;

namespace BookCatAPI.Models
{
    public class Library
    {
        public int Id { get; set; }
        [Column("user_id")]
        public int UserId { get; set; }
        [Column("plan_id")]
        public int? PlanId { get; set; }
        [Column("start_inventory")]
        public int StartInventory { get; set; } = 0;
        [Column("data_end_plan")]
        public DateTime? DataEndPlan { get; set; }
        public string Status { get; set; } = "active";
        [Column("dark_theme")]
        public bool DarkTheme { get; set; } = false;
        [Column("create_at")]
        public DateTime CreateAt { get; set; } = DateTime.Now;

        public User User { get; set; }
        public Plan? Plan { get; set; }
        public ICollection<Book> Books { get; set; }
        public ICollection<Document> Documents { get; set; }
    }

}
