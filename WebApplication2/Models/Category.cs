using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models;

public class Category : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; }
    
    [MaxLength(500)]
    public string Description { get; set; }
    
    public bool IsActive { get; set; } = true;
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}