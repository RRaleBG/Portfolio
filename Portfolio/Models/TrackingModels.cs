using System;
using System.ComponentModel.DataAnnotations;

namespace Portfolio.Models
{
    public class PageVisit
    {
        public int Id { get; set; }
        [Required]
        public string Page { get; set; } = "";
        [Required]
        public string IpAddress { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class MailSendLog
    {
        public int Id { get; set; }
        [Required]
        public string Recipient { get; set; } = "";
        public string Subject { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
