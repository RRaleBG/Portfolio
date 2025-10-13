using System.ComponentModel.DataAnnotations;

namespace Portfolio.Models
{
    public class Chat
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = string.Empty;
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Content { get; set; } = string.Empty;

        [StringLength(2000)]
        public string Response { get; set; } = string.Empty;

        [StringLength(100)]
        public string ChatSessionId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
