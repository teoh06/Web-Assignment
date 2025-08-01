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
            .Select(g => new { Date = g.Key, Total = g.Sum(o => o.OrderItems.Sum(i => i.UnitPrice * i.Quantity)) })
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
            .Select(g => new { Date = g.Key, Total = g.Sum(o => o.OrderItems.Sum(i => i.UnitPrice * i.Quantity)) })
            .OrderBy(x => x.Date)
            .ToList();

        var pdf = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Header().Text("Sales Report").FontSize(20).Bold().AlignCenter();
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(120);
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        header.Cell().Text("Date").Bold();
                        header.Cell().Text("Total Sales").Bold();
                    });
                    foreach (var s in sales)
                    {
                        table.Cell().Text(((DateTime)s.Date).ToString("yyyy-MM-dd"));
                        table.Cell().Text(((decimal)s.Total).ToString("C", new CultureInfo("en-MY")));
                    }
                });
            });
        });
        var stream = new MemoryStream();
        pdf.GeneratePdf(stream);
        stream.Position = 0;
        return File(stream.ToArray(), "application/pdf", "SalesReport.pdf");
    }
}
