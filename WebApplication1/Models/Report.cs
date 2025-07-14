using System.ComponentModel.DataAnnotations;

public class Report
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; }

    public string Description { get; set; }

    [MaxLength(100)]
    public string Category { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending, in_progress, resolved

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public User User { get; set; }
    public List<Response> Responses { get; set; } = [];
    public List<Attachment> Attachments { get; set; } = [];
}
