using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Portfolio.Models
{
    public class Project
    {
        public int Id { get; set; }
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;
        [StringLength(1000)]
        public string? Summary { get; set; }
        [StringLength(200)]
        public string? Url { get; set; }
        [StringLength(200)]
        public string? GitHubUrl { get; set; }
        [StringLength(200)]
        public string? ImageUrl { get; set; }
        [StringLength(200)]
        public string? Tags { get; set; }
        public DateTime? Date { get; set; }
        public string? OwnerId { get; set; }
        public IdentityUser? Owner { get; set; }
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
