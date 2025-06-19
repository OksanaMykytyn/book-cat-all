namespace BookCatAPI.Models.DTOs
{
    public class UserChangePasswordDto
    {
        public string Email { get; set; }
        public string NewPassword { get; set; }
    }
}
