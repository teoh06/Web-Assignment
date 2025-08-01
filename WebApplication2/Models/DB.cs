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
    public DbSet<Member> Members { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<MenuItemRating> MenuItemRatings { get; set; }
    public DbSet<MenuItemComment> MenuItemComments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Other configurations (e.g., for User inheritance if not done via TPT/TPH convention)
        // modelBuilder.Entity<Admin>().ToTable("Admins");
        // modelBuilder.Entity<Member>().ToTable("Members");
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
}

public class Admin : User
{
    // Additional admin-specific fields can go here
}

public class Member : User
{
    [MaxLength(100)]
    public string? PhotoURL { get; set; }
    // Navigation property for orders
    public ICollection<Order> Orders { get; set; }
}

public class Category
{
    [Key]
    public int CategoryId { get; set; }
    [Required(ErrorMessage ="Category Name is Required"), MaxLength(100)]
    public string Name { get; set; }
    // Navigation property
    public ICollection<MenuItem>? MenuItems { get; set; }
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
    [Required(ErrorMessage = "Category is required")]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
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
    public ICollection<OrderItem> OrderItems { get; set; }
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

// SmtpSetting class does NOT have a [Key] attribute because it's a keyless entity
public class SmtpSettings
{
    public string User { get; set; }
    public string Pass { get; set; }
    public string Name { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
}