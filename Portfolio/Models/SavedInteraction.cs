using System.ComponentModel.DataAnnotations;

namespace Portfolio.Models
{
    public class SavedInteraction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Question { get; set; } = string.Empty;

        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Answer { get; set; } = string.Empty;

        [StringLength(100)]
        public string? UserId { get; set; }

        [StringLength(100)]
        public string? Source { get; set; }

        [Range(1, 5)]
        public int? UserRating { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Add visitor IP address to track who made the interaction and store to db. update database accordingly
        [StringLength(45)] // IPv6 max length is 45 characters
        public string? VisitorIp { get; set; }
    }
}
