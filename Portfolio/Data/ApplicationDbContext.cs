using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Portfolio.Models;

namespace Portfolio.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Project> Projects => Set<Project>();
        public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
        public DbSet<Contact> Contacts => Set<Contact>();
        public DbSet<Chat> Chats => Set<Chat>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<SavedInteraction> SavedInteractions => Set<SavedInteraction>();
    }
}
