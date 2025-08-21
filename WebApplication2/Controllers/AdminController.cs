using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System;
using WebApplication2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication2.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;

namespace WebApplication2.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly DB _context;

    public AdminController(DB _context)
    {
        this._context = _context ;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult SalesSummary()
    {
        var sales = _context.Orders
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Sum(o => o.OrderItems.Sum(i => i.UnitPrice * i.Quantity)),
                Items = g.SelectMany(o => o.OrderItems)
                         .GroupBy(i => i.MenuItem.Name)
                         .Select(ig => new
                         {
                             Product = ig.Key,
                             Quantity = ig.Sum(x => x.Quantity),
                             Amount = ig.Sum(x => x.UnitPrice * x.Quantity)
                         })
                         .ToList()
            })
            .OrderBy(x => x.Date)
            .ToList();

        return PartialView("_SalesSummary", sales);
    }


    [HttpGet]
    public IActionResult SalesReportSection()
    {
        return PartialView("_SalesReportSection");
    }

    public IActionResult ManageUsers()
    {
        var admin = _context.Admins.ToList<User>();
        var member = _context.Members.ToList<User>();
        var all = admin.Concat(member).ToList();
        return PartialView("_ManageUsers", all);
    }

    public IActionResult SalesReport()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var sales = _context.Orders
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Sum(o => o.OrderItems.Sum(i => i.UnitPrice * i.Quantity)),
                Items = g.SelectMany(o => o.OrderItems)
                         .GroupBy(i => i.MenuItem.Name)
                         .Select(ig => new
                         {
                             Product = ig.Key,
                             Quantity = ig.Sum(x => x.Quantity),
                             Amount = ig.Sum(x => x.UnitPrice * x.Quantity)
                         })
                         .ToList()
            })
            .OrderBy(x => x.Date)
            .ToList();

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);

                page.Header()
                    .Text("Sales Report")
                    .FontSize(20)
                    .Bold()
                    .AlignCenter();

                page.Content().Column(col =>
                {
                    foreach (var day in sales)
                    {
                        // Day header
                        col.Item().Text($"{day.Date:yyyy-MM-dd} - Total: {day.Total.ToString("C", new CultureInfo("en-MY"))}")
                            .Bold()
                            .FontSize(14)
                            .Underline();

                        // Table of products
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Product
                                columns.RelativeColumn(1); // Quantity
                                columns.RelativeColumn(2); // Amount
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Text("Product").Bold();
                                header.Cell().Text("Quantity").Bold();
                                header.Cell().Text("Amount").Bold();
                            });

                            // Rows
                            foreach (var item in day.Items)
                            {
                                table.Cell().Text(item.Product);
                                table.Cell().Text(item.Quantity.ToString());
                                table.Cell().Text(item.Amount.ToString("C", new CultureInfo("en-MY")));
                            }
                        });

                        // Spacer between days
                        col.Item().Text("");
                    }
                });
            });
        });

        using var stream = new MemoryStream();
        pdf.GeneratePdf(stream);
        stream.Position = 0;

        return File(stream.ToArray(), "application/pdf", "SalesReport.pdf");
    }

    [HttpGet]
    public IActionResult SalesChartData()
    {
        var data = _context.Orders
            .SelectMany(o => o.OrderItems)
            .GroupBy(i => i.MenuItem.Category.Name)
            .Select(g => new {
                Category = g.Key,
                Total = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(x => x.Total)
            .ToList();
        return Json(new {
            labels = data.Select(x => x.Category).ToArray(),
            values = data.Select(x => (double)x.Total).ToArray()
        });
    }
    [HttpGet]
    public IActionResult MembersChartData()
    {
        var adminCount = _context.Admins.Count();
        var memberCount = _context.Members.Count();
        var total = adminCount + memberCount;
        return Json(new {
            labels = new[] { "Admins", "Members" },
            values = new[] { adminCount, memberCount }
        });
    }

    [HttpGet]
    public IActionResult TopMenuChartData()
    {
        var data = _context.OrderItems
            .GroupBy(i => i.MenuItem.Name)
            .Select(g => new {
                Name = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .OrderByDescending(x => x.Quantity)
            .Take(8)
            .ToList();
        return Json(new {
            labels = data.Select(x => x.Name).ToArray(),
            values = data.Select(x => x.Quantity).ToArray()
        });
    }

    // --- Order Maintenance ---
    // GET: /Admin/Orders
    public async Task<IActionResult> Orders(string status)
    {
        var orders = _context.Orders.Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).AsQueryable();
        if (!string.IsNullOrEmpty(status))
            orders = orders.Where(o => o.Status == status);
        return View(await orders.OrderByDescending(o => o.OrderDate).ToListAsync());
    }

    // GET: /Admin/OrderDetail/{id}
    public async Task<IActionResult> OrderDetail(int id)
    {
        var order = await _context.Orders.Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).FirstOrDefaultAsync(o => o.OrderId == id);
        if (order == null) return NotFound();
        return View(order);
    }

    // GET: /Admin/UpdateOrderStatus/{id}
    public async Task<IActionResult> UpdateOrderStatus(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        return View(order);
    }

    // POST: /Admin/UpdateOrderStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(Order model)
    {
        var order = await _context.Orders.FindAsync(model.OrderId);
        if (order == null) return NotFound();
        order.Status = model.Status;
        await _context.SaveChangesAsync();
        // --- SMS/Message notification stub ---
        TempData["Success"] = $"Order status updated to {model.Status}. (Notification sent to customer)";
        return RedirectToAction("UpdateOrderStatus", new { id = model.OrderId });
    }
    
    // POST: /Admin/UpdateOrderStatusAjax
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateOrderStatusAjax(int orderId, string status)
    {
        try
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return Json(new { success = false, message = "Order not found" });
            
            // Validate status
            var validStatuses = new[] { "Pending", "Paid", "Preparing", "Out for Delivery", "Delivered", "Cancelled", "Refunded" };
            if (string.IsNullOrEmpty(status) || !validStatuses.Contains(status))
            {
                return Json(new { success = false, message = "Invalid status" });
            }
            
            order.Status = status;
            await _context.SaveChangesAsync();
            
            // Get member email for notification
            var memberEmail = order.MemberEmail ?? string.Empty;
            
            // TODO: Send notification to member (SMS or email)
            // This would be implemented with a real notification service in production
            
            return Json(new { 
                success = true, 
                message = $"Order status updated to {status}",
                orderId = orderId,
                status = status
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error updating order status: {ex.Message}" });
        }
    }
    
    // GET: /Admin/GetOrderDetails
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetOrderDetails(int orderId)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
                
            if (order == null) return Json(new { success = false, message = "Order not found" });
            
            // Format order details for JSON response
            var orderDetails = new
            {
                orderId = order.OrderId,
                memberEmail = order.MemberEmail ?? string.Empty,
                orderDate = order.OrderDate.ToString("yyyy-MM-dd HH:mm:ss"),
                status = order.Status ?? "Unknown",
                paymentMethod = order.PaymentMethod ?? "Unknown",
                deliveryAddress = order.DeliveryAddress ?? "Not specified",
                deliveryOption = order.DeliveryOption ?? "Standard",
                items = order.OrderItems.Select(oi => new
                {
                    name = oi.MenuItem?.Name ?? "Unknown Item",
                    quantity = oi.Quantity,
                    unitPrice = oi.UnitPrice,
                    totalPrice = oi.Quantity * oi.UnitPrice
                }).ToList(),
                totalAmount = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice)
            };
            
            return Json(new { success = true, order = orderDetails });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error retrieving order details: {ex.Message}" });
        }
    }
    
    // Example of using OTP for a sensitive admin action
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        // Instead of directly deleting, redirect to OTP verification
        string returnUrl = Url.Action("ConfirmDeleteUser", "Admin", new { id });
        return RedirectToAction("RequestOtp", "Account", new { 
            email = User.Identity?.Name ?? string.Empty, 
            action = "delete a user account", 
            returnUrl 
        });
    }

    // This action will be called after OTP verification
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConfirmDeleteUser(string id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        
        // Now perform the actual deletion
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        
        TempData["Success"] = "User deleted successfully.";
        return RedirectToAction("_ManageUsers");
    }

    [HttpPost]
    public async Task<IActionResult> PromoteToAdmin(string email)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == email);
        if (member == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("ManageUsers", "Account");
        }

        _context.Members.Remove(member);

        var admin = new Admin
        {
            Email = member.Email,
            Name = member.Name,
            Hash = member.Hash,
            IsPendingDeletion = member.IsPendingDeletion,
            DeletionRequestDate = member.DeletionRequestDate,
            DeletionToken = member.DeletionToken,
            OtpCode = member.OtpCode,
            OtpExpiry = member.OtpExpiry
        };

        _context.Admins.Add(admin);
        await _context.SaveChangesAsync();

        TempData["Message"] = $"{admin.Name} already update to Admin";
        return RedirectToAction("Index", "Admin"); 
    }

    [HttpPost]
    public async Task<IActionResult> PromoteToAdmin(string email)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == email);
        if (member == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("ManageUsers", "Account");
        }

        _context.Members.Remove(member);

        var admin = new Admin
        {
            Email = member.Email,
            Name = member.Name,
            Hash = member.Hash,
            IsPendingDeletion = member.IsPendingDeletion,
            DeletionRequestDate = member.DeletionRequestDate,
            DeletionToken = member.DeletionToken,
            OtpCode = member.OtpCode,
            OtpExpiry = member.OtpExpiry
        };

        _context.Admins.Add(admin);
        await _context.SaveChangesAsync();

        TempData["Message"] = $"{admin.Name} already update to Admin";
        return RedirectToAction("Index", "Admin"); 
    }
}


