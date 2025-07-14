using Azure;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace Assignment.Models;

#nullable disable warnings

public class DB : DbContext
{
    public DB(DbContextOptions options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<Response> Responses { get; set; }
    public DbSet<Attachment> Attachments { get; set; }

    // 👇 加在 DB 类的大括号里面
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Prevent cascade delete from Admin → Response
        modelBuilder.Entity<Response>()
            .HasOne(r => r.Admin)
            .WithMany(u => u.Responses)
            .HasForeignKey(r => r.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
