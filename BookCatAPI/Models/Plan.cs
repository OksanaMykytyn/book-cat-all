namespace BookCatAPI.Models
{
    public class Plan
    {
        public int Id { get; set; }
        public int MaxBooks { get; set; }
        public decimal Price { get; set; }

        public ICollection<Library> Libraries { get; set; }
    }

}
