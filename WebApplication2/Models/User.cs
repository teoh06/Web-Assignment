using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models;

public class User : BaseEntity
{
    [Required, MaxLength(100)]
    public string Email { get; set; }
    
    [Required, MaxLength(100)]
    public string PasswordHash { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; }
    
    [Required, MaxLength(50)]
    public string RoleType { get; set; } // "Admin", "Member", "Staff"
    public string? PhotoPath { get; set; }

    public ICollection<Report> Reports { get; set; } = new List<Report>();
}