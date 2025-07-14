
using System.ComponentModel.DataAnnotations;

public class Attachment
{
    [Key]
    public int Id { get; set; }

    public int ReportId { get; set; }

    [MaxLength(255)]
    public string FilePath { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;

    // Navigation
    public Report Report { get; set; }
}
