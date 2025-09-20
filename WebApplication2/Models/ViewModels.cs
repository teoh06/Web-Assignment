using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using WebApplication2.Attributes;
using WebApplication2.Controllers; // Add this for CartItemVM, OrderItemVM

#nullable disable warnings

namespace WebApplication2.Models;

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

    public IFormFile? ProfilePicture { get; set; }

    // Address for order tracking
    [MaxLength(200)]
    public string? Address { get; set; } 
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
    public string Email { get; set; } = "";

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    public string PhotoURL { get; set; } = "";

    public IFormFile? ProfilePicture { get; set; }

    // Address for order tracking
    [ValidAddress(MinimumLength = 10, MaximumLength = 200, RequireHouseNumber = true, RequireStreetName = true)]
    [CompleteAddress(RequireCity = true, RequireStateProvince = false, RequirePostalCode = false)]
    public string? Address { get; set; }
    
    // Phone number for order notifications
    [Display(Name = "Phone Number")]
    [RegularExpression(@"^\d{10,15}$", ErrorMessage = "Please enter a valid phone number (10-15 digits)")]
    [MaxLength(15)]
    public string? PhoneNumber { get; set; }

    // For selecting from photo history
    public string? SelectedPhotoPath { get; set; }

    // For processed image data (base64)
    public string? ProcessedImageData { get; set; }
    public List<ProfilePhotoVM>? PhotoHistory { get; set; }
}

public class ProfilePhotoVM
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public DateTime UploadDate { get; set; }
}


public class MenuItemIndexVM
{
    public List<MenuItem> MenuItems { get; set; }
    public List<Category> Categories { get; set; }
}
public class MenuItemDetailsVM
{
    public MenuItem MenuItem { get; set; }
    public List<MenuItemRating> Ratings { get; set; }
    public List<MenuItemComment> Comments { get; set; }
    public double AverageRating => Ratings?.Count > 0 ? Ratings.Average(r => r.Value) : 0;
    public int RatingsCount => Ratings?.Count ?? 0;
    // Whether the currently logged-in member is allowed to rate (has purchased before)
    public bool CanRate { get; set; } = false;
}

public class TrackOrderVM
{
    [Required]
    public string OrderNumber { get; set; }
    public string? Address { get; set; }
    public List<OrderDetailsVM> Orders { get; set; } = new List<OrderDetailsVM>();
    public bool IsPostBack { get; set; }
}

public class OrderDetailsVM
{
    public string OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; }
    public string DeliveryOption { get; set; } // Add delivery option for tracking
    public List<OrderItemVM> Items { get; set; } = new List<OrderItemVM>(); // Add items for tracking view
}

public class PaymentVM
{
    [Display(Name = "Payment Method")]
    [Required(ErrorMessage = "Please select a payment method.")]
    public string PaymentMethod { get; set; }

    [Display(Name = "Card Number")]
    [RegularExpression(@"^\d{16}$", ErrorMessage = "Card number must be 16 digits.")]
    public string? CardNumber { get; set; }

    [Display(Name = "Card Holder Name")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string? CardHolderName { get; set; }

    [Display(Name = "Expiry Date")]
    [RegularExpression(@"^(0[1-9]|1[0-2])\/([0-9]{2})$", ErrorMessage = "Expiry date must be in MM/YY format")]
    public string? ExpiryDate { get; set; }

    [Display(Name = "CVV")]
    [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits")]
    public string? CVV { get; set; }

    [Display(Name = "Billing Address")]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
    public string? BillingAddress { get; set; }

    // --- Enhancement: Delivery Address for this order ---
    [Display(Name = "Delivery Address")]
    [Required(ErrorMessage = "Delivery address is required.")]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
    public string DeliveryAddress { get; set; }

    [Display(Name = "Phone Number")]
    [RegularExpression(@"^\d{10,12}$", ErrorMessage = "Please enter a valid phone number")]
    [Required(ErrorMessage = "Phone number is required for order updates")]
    public string PhoneNumber { get; set; }

    [Display(Name = "Delivery Instructions")]
    [StringLength(500, ErrorMessage = "Delivery instructions cannot exceed 500 characters")]
    public string? DeliveryInstructions { get; set; }

    [Display(Name = "Delivery Option")]
    [Required(ErrorMessage = "Please select a delivery option.")]
    public string DeliveryOption { get; set; } // "Delivery" or "Pickup"

    public decimal Total { get; set; } // Set by the controller based on session cart
    public List<CartItemVM> CartItems { get; set; } = new List<CartItemVM>(); // Add this property
}

public class ReceiptVM
{
    public int OrderId { get; set; }
    public DateTime Date { get; set; }
    public List<CartItemVM> Items { get; set; } = new List<CartItemVM>(); // Initialize list
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } // Add payment method
    public string MemberEmail { get; set; }   // Add member email
    public string Status { get; set; }        // Add order status

    // New fields from payment information
    public string PhoneNumber { get; set; }
    public string DeliveryInstructions { get; set; }
    public string CardNumber { get; set; }  // Only last 4 digits will be displayed
    public string DeliveryOption { get; set; } // Add delivery option
    // --- Enhancement: Show delivery address used ---
    public string DeliveryAddress { get; set; }
}

public class OrderSummaryVM // A nested ViewModel for each individual order in the history
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
    public List<OrderItemVM> Items { get; set; } = new List<OrderItemVM>(); // Details for each item in the order
    public string DeliveryAddress { get; set; }
    public string DeliveryOption { get; set; } // Add delivery option
}

public class OtpVerificationVM
{
    [Required]
    public string Email { get; set; }
    
    [Required(ErrorMessage = "Please enter the verification code")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be 6 digits")]
    [Display(Name = "Verification Code")]
    public string OtpCode { get; set; }
    
    public string Action { get; set; }
    public string ReturnUrl { get; set; }
}

public class CartItemInputModel
{
    public int menuItemId { get; set; }
    public int quantity { get; set; }
    public string? SelectedPersonalizations { get; set; }
}

public class CartUpdateModel
{
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
}

public class CartItemVM
{
    public int MenuItemId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string PhotoURL { get; set; }
    public string? SelectedPersonalizations { get; set; }
    public int? UserRating { get; set; } // User's rating for this item (1-5, null if not rated)
}



// ReceiptVM is now defined in Models/ViewModels.cs

public class OrderHistoryVM
{
    public List<OrderSummaryVM> Orders { get; set; } = new List<OrderSummaryVM>();
}


public class OrderItemVM
{
    public string MenuItemName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string PhotoURL { get; set; }
    public string? SelectedPersonalizations { get; set; }
}

public class OrderRefundVM
{
    [Required]
    [Display(Name = "Order Number")]
    public int OrderId { get; set; }

    [Required]
    [StringLength(500)]
    [Display(Name = "Reason for Refund")]
    public string Reason { get; set; }
}

public class FeaturedMenuItemVM
{
    public int MenuItemId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string Image { get; set; }
}