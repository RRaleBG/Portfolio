using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Portfolio.Models
{
    public class BlogPost
    {
        public int Id { get; set; }
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Slug { get; set; } = string.Empty;
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;
        [Required]
        [StringLength(5000, MinimumLength = 10)]
        public string Content { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

        public string? AuthorId { get; set; }
        public IdentityUser? Author { get; set; }
          
        public List<Comment> Comments { get; set; } = new List<Comment>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
