using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;

namespace Portfolio.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 3)]
        public string CommentText { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Stars { get; set; }

        // Foreign key for Project
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        // Foreign key for Blog
        public int? BlogId { get; set; }
        public BlogPost? Blog { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
     
        

    }
}
