using System.ComponentModel.DataAnnotations;

public class Response
{
    [Key]
    public int Id { get; set; }

    public int ReportId { get; set; }

    public int AdminId { get; set; } // FK to User

    public string Message { get; set; }

    public DateTime RespondedAt { get; set; } = DateTime.Now;

    // Navigation
    public Report Report { get; set; }
    public User Admin { get; set; }
}
