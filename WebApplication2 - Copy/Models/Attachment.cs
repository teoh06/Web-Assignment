using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models;

public class Attachment : BaseEntity
{
    [Required, MaxLength(255)]
    public string FilePath { get; set; }
    
    public int ReportId { get; set; }
    [ForeignKey("ReportId")]
    public Report Report { get; set; }
}