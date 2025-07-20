using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models;

public class Report : BaseEntity
{
    [Required, MaxLength(200)]
    public string Title { get; set; }
    
    [Required, MaxLength(1000)]
    public string Description { get; set; }
    
    [MaxLength(255)]
    public string PhotoURL { get; set; }
    
    public int SubmittedById { get; set; }
    [ForeignKey("SubmittedById")]
    public User SubmittedBy { get; set; }
    
    public string Status { get; set; } = "Pending";
    public string Priority { get; set; } = "Normal";
    
    public int CategoryId { get; set; }
    [ForeignKey("CategoryId")]
    public Category Category { get; set; }
    
    public int LocationId { get; set; }
    [ForeignKey("LocationId")]
    public Location Location { get; set; }
    
    public ICollection<Response> Responses { get; set; } = new List<Response>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}