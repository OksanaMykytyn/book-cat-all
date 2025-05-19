using System.ComponentModel.DataAnnotations.Schema;

namespace BookCatAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Userlogin { get; set; }
        public string Userpassword { get; set; }
        public string? Userimage { get; set; }
        [Column("create_at")]
        public DateTime CreateAt { get; set; } = DateTime.Now;

        public ICollection<Library> Libraries { get; set; }
    }
}
