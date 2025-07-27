using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

#nullable disable warnings

namespace Demo.Models;

public class RegisterVM
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [Remote("CheckEmail", controller:"Account", ErrorMessage = "Duplicated Email.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password cannot be empty.")]
    [StringLength(100, MinimumLength = 5, ErrorMessage = "Password must be between 6 and 100 characters long.")]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [Required(ErrorMessage = "Confirm Password cannot be empty.")]
    [Compare("Password", ErrorMessage = "Password and confirm password not match.")]
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Confirm { get; set; }

    [Required]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; }

    public IFormFile ProfilePicture { get; set; } 
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
    [StringLength(100)]
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

public class UpdateProfileVM
{
    public string? Email { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public string? PhotoURL { get; set; }

    public IFormFile? ProfilePicture { get; set; }
}

public class FeaturedMenuItemVM
{
    public string Image { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
}
