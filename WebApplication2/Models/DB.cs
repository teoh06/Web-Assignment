using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace WebApplication2.Models;

#nullable disable warnings

public class DB : DbContext
{
    public DB(DbContextOptions options) : base(options) { }

    // DB Sets
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Chef> Chefs { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<MenuItemRating> MenuItemRatings { get; set; }
    public DbSet<MenuItemComment> MenuItemComments { get; set; }
    public DbSet<MemberPhoto> MemberPhotos { get; set; }
    public DbSet<PersonalizationOption> PersonalizationOptions { get; set; } // Added DbSet for PersonalizationOption
    public DbSet<MenuItemImage> MenuItemImages { get; set; } // Added DbSet for MenuItemImage
    public DbSet<MenuItemFavorite> MenuItemFavorites { get; set; } // Added for favorites
    public DbSet<CartItem> CartItems { get; set; } // Add CartItems for top sell logic
    public DbSet<WishListItem> WishListItems { get; set; } // Add DbSet for WishListItem

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Other configurations (e.g., for User inheritance if not done via TPT/TPH convention)
        // modelBuilder.Entity<Admin>().ToTable("Admins");
        // modelBuilder.Entity<Member>().ToTable("Members");
        
        // MenuItem -> Category: optional relationship, set null on category delete
        modelBuilder.Entity<MenuItem>()
            .HasOne(m => m.Category)
            .WithMany(c => c.MenuItems)
            .HasForeignKey(m => m.CategoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Add unique constraint to prevent duplicate orders within a short time window
        modelBuilder.Entity<Order>()
            .HasIndex(o => new { o.MemberEmail, o.OrderHash })
            .HasDatabaseName("IX_Orders_MemberEmail_OrderHash")
            .IsUnique(false); // Allow multiple orders with same hash, but we'll check in code
            
        // Add index for efficient duplicate checking
        modelBuilder.Entity<Order>()
            .HasIndex(o => new { o.MemberEmail, o.OrderDate })
            .HasDatabaseName("IX_Orders_MemberEmail_OrderDate");
    }
}

// Entity Classes -------------------------------------------------------------

public class User
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [Key, MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string Hash { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }

    public string Role => GetType().Name;

    // These fields for account deletion and recovery
    public bool IsPendingDeletion { get; set; } = false;
    public DateTime? DeletionRequestDate { get; set; }
    [MaxLength(100)]
    public string? DeletionToken { get; set; }
    
    // OTP related fields
    [MaxLength(6)]
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiry { get; set; }
}

public class Admin : User {}

public class Chef : User {}

public class Member : User
{
    [MaxLength(100)]
    public string? PhotoURL { get; set; }
    // Navigation property for orders
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    // New Address Field
    [MaxLength(200)]
    public string? Address { get; set; }
    
    // Phone number for order notifications
    [MaxLength(15)]
    public string? PhoneNumber { get; set; }

    // Navigation property for photo history
    public ICollection<MemberPhoto> MemberPhotos { get; set; } = new List<MemberPhoto>();
}

public class MemberPhoto
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string MemberEmail { get; set; }
    [ForeignKey("MemberEmail")]
    public Member Member { get; set; }
    public string FileName { get; set; }
    public DateTime UploadDate { get; set; }
}

public class Category
{
    [Key]
    public int CategoryId { get; set; }
    [Required(ErrorMessage ="Category Name is Required"), MaxLength(100)]
    public string Name { get; set; }
    // Navigation property
    public ICollection<MenuItem>? MenuItems { get; set; } = new List<MenuItem>();
}

public class MenuItem
{
    [Key]
    public int MenuItemId { get; set; }
    [Required(ErrorMessage = "Name is required"), MaxLength(100)]
    public string Name { get; set; }
    [MaxLength(500)]
    [Required(ErrorMessage = "Description is required")]
    public string Description { get; set; }
    [Required]
    [Range(0.01, 9999, ErrorMessage = "Price must be more than 0")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
    public string? PhotoURL { get; set; }

    // Foreign key
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<MenuItemImage> MenuItemImages { get; set; } = new List<MenuItemImage>();

    // --- Add IsActive property for menu item filtering ---
    public bool IsActive { get; set; } = true;
    
    // --- Add soft delete functionality to preserve order history ---
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; } // Track who deleted the item

    // --- Add ratings navigation property ---
    public ICollection<MenuItemRating> MenuItemRatings { get; set; } = new List<MenuItemRating>();
    
    // --- Add stock tracking ---
    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative")]
    public int StockQuantity { get; set; } = 0;
    
    // --- Track if item is out of stock ---
    public bool IsOutOfStock => StockQuantity <= 0;
}


public class Order
{
    [Key]
    public int OrderId { get; set; }
    [Required]
    public string MemberEmail { get; set; }
    [ForeignKey("MemberEmail")]
    public Member Member { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Now;
    public string Status { get; set; } // e.g., Pending, Preparing, Served
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    // Add persistent payment method
    public string PaymentMethod { get; set; } // Cash, Card, etc.
    // --- Enhancement: Store delivery address used for this order ---
    [MaxLength(200)]
    public string DeliveryAddress { get; set; }
    public string DeliveryOption { get; set; } // Add delivery option

    // --- Persistent member info for sales tracking ---
    [MaxLength(100)]
    public string MemberName { get; set; } // Store member name at time of order
    [MaxLength(15)]
    public string MemberPhone { get; set; } // Store member phone at time of order

    // --- Store delivery instructions and card number for receipt display ---
    [MaxLength(500)]
    public string? DeliveryInstructions { get; set; } // Store delivery instructions
    [MaxLength(16)]
    public string? CardNumber { get; set; } // Store last 4 digits of card number

    // --- Anti-duplicate order protection ---
    [MaxLength(64)]
    public string? OrderHash { get; set; } // Hash of cart contents for duplicate detection
    
    // --- Status modification tracking ---
    public int StatusModificationCount { get; set; } = 0; // Track total number of status changes
    public DateTime? LastStatusChangeDate { get; set; } // Track when status was last changed
    public string? PreviousStatus { get; set; } // Track previous status for audit
    
    // --- Role-specific modification tracking ---
    public int ChefModificationCount { get; set; } = 0; // Track Chef modifications
    public int AdminModificationCount { get; set; } = 0; // Track Admin modifications
    public string? LastModifiedByRole { get; set; } // Track which role made the last change
    
    // --- Helper properties for business rules ---
    public bool IsFinalStatus => Status == "Refunded" || Status == "Delivered" || Status == "Declined" || Status == "Ready for Pickup";
    public bool CanModifyStatus => StatusModificationCount < 2 && !IsFinalStatus;
    public bool CanChefModify => ChefModificationCount == 0 && StatusModificationCount < 2 && !IsFinalStatus;
    public bool CanAdminModify => AdminModificationCount == 0 && StatusModificationCount < 2 && !IsFinalStatus;
}

public class OrderItem
{
    [Key]
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; }
    [Range(1, 100)]
    public int Quantity { get; set; }
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }
    [MaxLength(500)]
    public string? SelectedPersonalizations { get; set; } // Comma-separated option names
}

public class MenuItemRating
{
    [Key]
    public int RatingId { get; set; }
    [Range(1,5)]
    public int Value { get; set; }
    [Required]
    public string MemberEmail { get; set; }
    [ForeignKey("MemberEmail")]
    public Member Member { get; set; }
    public int MenuItemId { get; set; }
    [ForeignKey("MenuItemId")]
    public MenuItem MenuItem { get; set; }
    public DateTime RatedAt { get; set; } = DateTime.Now;
}

public class MenuItemComment
{
    [Key]
    public int CommentId { get; set; }
    [Required]
    public string MemberEmail { get; set; }
    [ForeignKey("MemberEmail")]
    public Member Member { get; set; }
    public int MenuItemId { get; set; }
    [ForeignKey("MenuItemId")]
    public MenuItem MenuItem { get; set; }
    [Required, MaxLength(500)]
    public string Content { get; set; }
    public DateTime CommentedAt { get; set; } = DateTime.Now;
}

public class PersonalizationOption
{
    [Key]
    public int Id { get; set; }
    public int CategoryId { get; set; }
    [ForeignKey("CategoryId")]
    public Category Category { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; }
}

public class MenuItemImage
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    [ForeignKey("MenuItemId")]
    public MenuItem MenuItem { get; set; }
    [MaxLength(100)]
    public string FileName { get; set; }
    public DateTime UploadDate { get; set; }
}

public class MenuItemFavorite
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; }
    public string MemberEmail { get; set; }
    [ForeignKey("MemberEmail")]
    public Member Member { get; set; }
}

public class CartItem
{
    [Key]
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; }
    public string MemberEmail { get; set; }
    [ForeignKey("MemberEmail")]
    public Member Member { get; set; }
    [Range(1, 100)]
    public int Quantity { get; set; } = 1;
}

public class WishListItem
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; }
    public string MemberEmail { get; set; }
    [ForeignKey("MemberEmail")]
    public Member Member { get; set; }
}

// SmtpSetting class does NOT have a [Key] attribute because it's a keyless entity
public class SmtpSettings
{
    public string User { get; set; }
    public string Pass { get; set; }
    public string Name { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
}