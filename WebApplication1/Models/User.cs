using Azure;
using System.ComponentModel.DataAnnotations;

public class User
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(100)]
    public string Email { get; set; }

    [MaxLength(255)]
    public string Password { get; set; }

    [MaxLength(20)]
    public string Role { get; set; } // e.g., "user", "admin"

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public List<Report> Reports { get; set; } = [];
    public List<Response> Responses { get; set; } = [];
}
