using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models;

public class Notification : BaseEntity
{
    [Required, MaxLength(100)]
    public string Email { get; set; }
    
    [Required, MaxLength(200)]
    public string Message { get; set; }
    
    public bool IsRead { get; set; }
    
    public int? ReportId { get; set; }
    [ForeignKey("ReportId")]
    public Report Report { get; set; }
}