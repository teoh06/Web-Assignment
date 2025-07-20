using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models;

public class Member : User
{
    [MaxLength(255)]
    public string PhotoURL { get; set; }
}