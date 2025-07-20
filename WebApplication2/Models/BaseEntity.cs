using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models;

public class BaseEntity
{
    [Key]
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}