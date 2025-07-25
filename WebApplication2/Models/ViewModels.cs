using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
#nullable disable warnings

namespace WebApplication2.Models;

public class RegisterVM
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password cannot be empty.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
    public string Password { get; set; }

    [Required(ErrorMessage = "Confirm Password cannot be empty.")]
    [Compare("Password", ErrorMessage = "Password and confirm password not match.")]
    public string ConfirmPassword { get; set; }

    [Required]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; }

    [Required(ErrorMessage = "Role type is required.")]
    public string RoleType { get; set; }

    public IFormFile? ProfilePicture { get; set; } 
}

public class LoginVM
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; }
    [Required(ErrorMessage = "Password cannot be empty.")]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}

public class ResetPasswordVM
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}

public class UpdatePasswordVM
{
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string Current { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string New { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("New")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }
}

public class EmailVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    public bool IsBodyHtml { get; set; }
}

public class DashboardVM
{
    public int TotalUsers { get; set; }
    public int TotalReports { get; set; }
    public int PendingReports { get; set; }
    public int ResolvedReports { get; set; }

    public List<Report> RecentReports { get; set; } 
}

public class ReportFormVM
{
    [Required(ErrorMessage = "Description is required")]
    public string Description { get; set; }

    [Required(ErrorMessage = "CategoryID is required")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "LocationID is required")]
    public int LocationId { get; set; }

    public List<IFormFile> Attachments { get; set; }

    public List<Category> Categories { get; set; }

    public List<Location> Locations { get; set; }
}