using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models;

public class Response : BaseEntity
{
    [Required, MaxLength(1000)]
    public string Message { get; set; }
    
    public int ReportId { get; set; }
    [ForeignKey("ReportId")]
    public Report Report { get; set; }
    
    public int RespondedById { get; set; }
    [ForeignKey("RespondedById")]
    public User RespondedBy { get; set; }
}