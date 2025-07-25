using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using X.PagedList.Extensions;
using Microsoft.AspNetCore.Authorization;
using WebApplication2.Models;

namespace WebApplication2.Controllers;

public class HomeController : Controller
{
    private readonly DB _dbContext;
    private readonly IWebHostEnvironment _hostingEnvironment;

    public HomeController(DB dbContext, IWebHostEnvironment hostingEnvironment)
    {
        _dbContext = dbContext;
        _hostingEnvironment = hostingEnvironment;
    }

    // GET: Home/Index
    public IActionResult Index()
    {
        return View();
    }


    [Authorize(Roles = "Admin, Member, Staff")]
    public IActionResult LoginIndex()
    {
        var model = new DashboardVM
        {
            TotalUsers = _dbContext.Users.Count(),
            TotalReports = _dbContext.Reports.Count(),
            PendingReports = _dbContext.Reports.Count(r => r.Status == "Pending"),
            ResolvedReports = _dbContext.Reports.Count(r => r.Status == "Resolved"),
            RecentReports = _dbContext.Reports
        .OrderByDescending(r => r.Id)
        .Take(5)
        .ToList()
        };
        return View(model);
    }

    // GET: Home/Both
    [Authorize]
    public IActionResult Both()
    {
        return View();
    }
    [Authorize(Roles = "Guest")]
    public IActionResult Guest()
    {
        return View();
    }

    // GET: Home/Member
    [Authorize(Roles = "Member")]
    public IActionResult Member()
    {
        return View();
    }

    // GET: Home/Admin
    [Authorize(Roles = "Admin")]
    public IActionResult Admin()
    {
        return View();
    }

    // GET: Home/Report
    public IActionResult Report()
    {
        return View();
    }

    // GET: Home/Privacy
    public IActionResult Privacy()
    {
        return View();
    }

    // POST: Home/SubmitReport
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReport(string userName, string description, List<IFormFile> attachments)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(description))
        {
            ModelState.AddModelError("", "User and Description are required.");
            return View("Report");
        }

        // Find user by userName (implement your user lookup logic here)
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Name == userName);
        if (user == null)
        {
            ModelState.AddModelError("", "User not found.");
            return View("Report");
        }

        var report = new Report
        {
            Title = description.Length > 50 ? description.Substring(0, 50) : description,
            Description = description,
            SubmittedById = user.Id,
            Status = "Pending",
            Priority = "Normal",
            // Set default CategoryId and LocationId or get from form if needed
            CategoryId = 1, // example
            LocationId = 1  // example
        };

        _dbContext.Reports.Add(report);
        await _dbContext.SaveChangesAsync();

        // Handle file attachments
        if (attachments != null && attachments.Count > 0)
        {
            foreach (var file in attachments)
            {
                if (file.Length > 0)
                {
                    // Save file to wwwroot/photos or other location
                    var fileName = Path.GetFileName(file.FileName);
                    var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "photos", fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var attachment = new Attachment
                    {
                        ReportId = report.Id,
                        FilePath = "/photos/" + fileName
                    };

                    _dbContext.Attachments.Add(attachment);
                }
            }
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToAction("Report");
    }

    [Authorize]
    public async Task<IActionResult> ReportHistory()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(); 
        }

        var reports = await _dbContext.Reports
        .Where(r => r.SubmittedById == userId)
        .OrderByDescending(r => r.Id)
        .ToListAsync();

        return View(reports);
    }


}
