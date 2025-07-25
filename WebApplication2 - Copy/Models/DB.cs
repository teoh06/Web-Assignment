using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;

namespace WebApplication2.Models;

public class DB : DbContext
{
    public DB(DbContextOptions options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<Response> Responses { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Location> Locations { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // User relationships
        builder.Entity<Report>()
            .HasOne(r => r.SubmittedBy)
            .WithMany(u => u.Reports)
            .HasForeignKey(r => r.SubmittedById);

        builder.Entity<Response>()
            .HasOne(r => r.RespondedBy)
            .WithMany()
            .HasForeignKey(r => r.RespondedById)
            .OnDelete(DeleteBehavior.Restrict); 

        // Report relationships
        builder.Entity<Report>()
            .HasOne(r => r.Category)
            .WithMany(c => c.Reports)
            .HasForeignKey(r => r.CategoryId);

        builder.Entity<Report>()
            .HasOne(r => r.Location)
            .WithMany(l => l.Reports)
            .HasForeignKey(r => r.LocationId);

        // Attachment relationship
        builder.Entity<Attachment>()
            .HasOne(a => a.Report)
            .WithMany(r => r.Attachments)
            .HasForeignKey(a => a.ReportId);

        // Add this to avoid multiple cascade paths
        builder.Entity<Response>()
            .HasOne(r => r.Report)
            .WithMany()
            .HasForeignKey(r => r.ReportId)
            .OnDelete(DeleteBehavior.Restrict); 
    }

}