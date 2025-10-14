using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Portfolio.Models;

namespace Portfolio.Database
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<IdentityUser>(options)
    {
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
        public DbSet<Contact> Contacts => Set<Contact>();
        public DbSet<Chat> Chats => Set<Chat>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<SavedInteraction> SavedInteractions => Set<SavedInteraction>();
        public DbSet<PageVisit> PageVisits => Set<PageVisit>();
        public DbSet<MailSendLog> MailSendLogs => Set<MailSendLog>();
    }
}
