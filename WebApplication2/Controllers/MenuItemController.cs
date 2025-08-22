using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebApplication2.Models;
using Microsoft.Extensions.Logging;

namespace WebApplication2.Controllers;

public class MenuItemController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment env;
    private readonly ILogger<MenuItemController> logger;

    public MenuItemController(DB db, IWebHostEnvironment env, ILogger<MenuItemController> logger)
    {
        this.db = db;
        this.env = env;
        this.logger = logger;
    }

    [AllowAnonymous]
    public IActionResult Index()
    {
        if (TempData["SuccessMessage"] != null)
        {
            ViewBag.SuccessMessage = TempData["SuccessMessage"];
        }

        IQueryable<MenuItem> query = db.MenuItems;

        // Hide inactive items for non-admins
        if (!User.IsInRole("Admin"))
        {
            query = query.Where(m => m.IsActive);
        }

        var items = query
            .Include(m => m.Category)
            .Include(m => m.MenuItemRatings) // Include ratings for average calculation
            .OrderBy(m => m.Name)
            .ToList();

        var vm = new MenuItemIndexVM
        {
            MenuItems = items,
            Categories = db.Categories.ToList()
        };

        return View(vm);
    }


    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        ViewBag.Categories = db.Categories.ToList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([Bind("Name,Description,Price,CategoryId,PhotoURL,StockQuantity")] MenuItem menuItem, IFormFile imageFile, string processedImageData)
    {
        logger.LogInformation("Create action called with MenuItem: {@MenuItem}", menuItem);
        logger.LogInformation("ImageFile is null: {IsNull}, ProcessedImageData is null: {ProcessedImageDataIsNull}", 
            imageFile == null, string.IsNullOrEmpty(processedImageData));

        if (!ModelState.IsValid)
        {
            logger.LogWarning("ModelState is invalid. Errors: {@Errors}",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            ViewBag.Categories = db.Categories.ToList();
            return View(menuItem);
        }

        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(menuItem.Name))
            {
                ModelState.AddModelError("Name", "Name is required.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            if (string.IsNullOrWhiteSpace(menuItem.Description))
            {
                ModelState.AddModelError("Description", "Description is required.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            if (menuItem.Price <= 0)
            {
                ModelState.AddModelError("Price", "Price must be greater than 0.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            if (menuItem.CategoryId <= 0)
            {
                ModelState.AddModelError("CategoryId", "Please select a category.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            // Handle image upload - prioritize processed image data over file upload
            if (!string.IsNullOrEmpty(processedImageData))
            {
                string? fileName = await SaveProcessedImage(processedImageData);
                if (fileName == null)
                {
                    ViewBag.Categories = db.Categories.ToList();
                    return View(menuItem);
                }
                menuItem.PhotoURL = fileName;
            }
            else if (imageFile != null)
            {
                string? fileName = await SaveImage(imageFile);
                if (fileName == null)
                {
                    // SaveImage adds the model error, so we just return the view.
                    ViewBag.Categories = db.Categories.ToList();
                    return View(menuItem);
                }
                menuItem.PhotoURL = fileName;
            }

            // Check if menu item name already exists in the same category
            var existingItem = db.MenuItems.FirstOrDefault(m =>
                m.Name.ToLower() == menuItem.Name.ToLower() &&
                m.CategoryId == menuItem.CategoryId);

            if (existingItem != null)
            {
                ModelState.AddModelError("Name", "A menu item with this name already exists in the selected category.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            logger.LogInformation("Adding MenuItem to database: {@MenuItem}", menuItem);
            db.MenuItems.Add(menuItem);
            var result = await db.SaveChangesAsync();
            logger.LogInformation("SaveChangesAsync result: {Result}", result);

            TempData["SuccessMessage"] = $"Menu item '{menuItem.Name}' was created successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating menu item: {Message}", ex.Message);
            ModelState.AddModelError("", $"Error creating menu item: {ex.Message}");
            ViewBag.Categories = db.Categories.ToList();
            return View(menuItem);
        }
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var menuItem = await db.MenuItems.FindAsync(id);
        if (menuItem == null)
        {
            return NotFound();
        }
        ViewBag.Categories = db.Categories.ToList();
        return View(menuItem);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, [Bind("MenuItemId,Name,Description,Price,CategoryId,PhotoURL,StockQuantity")] MenuItem menuItem, IFormFile? imageFile, bool removeImage = false, string processedImageData = null)
    {
        logger.LogInformation("Edit action called with id: {Id}, MenuItem: {@MenuItem}", id, menuItem);
        logger.LogInformation("ImageFile is null: {IsNull}, RemoveImage: {RemoveImage}, ProcessedImageData is null: {ProcessedImageDataIsNull}", 
            imageFile == null, removeImage, string.IsNullOrEmpty(processedImageData));

        if (id != menuItem.MenuItemId)
        {
            return NotFound();
        }

        // We need to fetch the original entity from the database first
        var menuItemToUpdate = await db.MenuItems.FindAsync(id);
        if (menuItemToUpdate == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            logger.LogWarning("ModelState is invalid. Errors: {@Errors}",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            ViewBag.Categories = db.Categories.ToList();
            return View(menuItem);
        }

        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(menuItem.Name))
            {
                ModelState.AddModelError("Name", "Name is required.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            if (string.IsNullOrWhiteSpace(menuItem.Description))
            {
                ModelState.AddModelError("Description", "Description is required.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            if (menuItem.Price <= 0)
            {
                ModelState.AddModelError("Price", "Price must be greater than 0.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            if (menuItem.CategoryId <= 0)
            {
                ModelState.AddModelError("CategoryId", "Please select a category.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            // Check if menu item name already exists in the same category (excluding current item)
            var existingItem = db.MenuItems.FirstOrDefault(m =>
                m.Name.ToLower() == menuItem.Name.ToLower() &&
                m.CategoryId == menuItem.CategoryId &&
                m.MenuItemId != id);

            if (existingItem != null)
            {
                ModelState.AddModelError("Name", "A menu item with this name already exists in the selected category.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }

            // Update fields
            menuItemToUpdate.Name = menuItem.Name;
            menuItemToUpdate.Description = menuItem.Description;
            menuItemToUpdate.Price = menuItem.Price;
            menuItemToUpdate.CategoryId = menuItem.CategoryId;
            menuItemToUpdate.StockQuantity = menuItem.StockQuantity;

            if (removeImage)
            {
                // Delete old photo if it exists and clear the URL
                if (!string.IsNullOrEmpty(menuItemToUpdate.PhotoURL))
                {
                    DeleteImage(menuItemToUpdate.PhotoURL);
                }
                menuItemToUpdate.PhotoURL = null;
            }
            else if (!string.IsNullOrEmpty(processedImageData))
            {
                // Delete old photo before saving the processed one
                if (!string.IsNullOrEmpty(menuItemToUpdate.PhotoURL))
                {
                    DeleteImage(menuItemToUpdate.PhotoURL);
                }
                // Save the processed photo
                string? fileName = await SaveProcessedImage(processedImageData);
                if (fileName == null)
                {
                    ViewBag.Categories = db.Categories.ToList();
                    return View(menuItem); // Return with error
                }
                menuItemToUpdate.PhotoURL = fileName;
            }
            else if (imageFile != null)
            {
                // Delete old photo before saving the new one
                if (!string.IsNullOrEmpty(menuItemToUpdate.PhotoURL))
                {
                    DeleteImage(menuItemToUpdate.PhotoURL);
                }
                // Save the new photo
                string? fileName = await SaveImage(imageFile);
                if (fileName == null)
                {
                    ViewBag.Categories = db.Categories.ToList();
                    return View(menuItem); // Return with error
                }
                menuItemToUpdate.PhotoURL = fileName;
            }
            // If neither removeImage nor a new imageFile is provided, we keep the existing PhotoURL.

            logger.LogInformation("Updating MenuItem in database: {@MenuItem}", menuItemToUpdate);
            var result = await db.SaveChangesAsync();
            logger.LogInformation("SaveChangesAsync result: {Result}", result);

            TempData["SuccessMessage"] = $"Menu item '{menuItem.Name}' was updated successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!db.MenuItems.Any(e => e.MenuItemId == menuItem.MenuItemId))
            {
                return NotFound();
            }
            else
            {
                ModelState.AddModelError("", "The menu item was modified by another user. Please refresh and try again.");
                ViewBag.Categories = db.Categories.ToList();
                return View(menuItem);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating menu item: {Message}", ex.Message);
            ModelState.AddModelError("", $"Error updating menu item: {ex.Message}");
            ViewBag.Categories = db.Categories.ToList();
            return View(menuItem);
        }
    }

    // *** FIX: Add a centralized helper method for saving images ***
    private async Task<string?> SaveImage(IFormFile imageFile)
    {
        logger.LogInformation("SaveImage called with file: {FileName}, Size: {Size}, ContentType: {ContentType}",
            imageFile.FileName, imageFile.Length, imageFile.ContentType);

        if (imageFile.Length == 0 || imageFile.Length > 2 * 1024 * 1024) // 2MB limit
        {
            ModelState.AddModelError("imageFile", "File size must be between 1 byte and 2MB.");
            return null;
        }

        // Check for valid image types
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
        {
            ModelState.AddModelError("imageFile", "Invalid file type. Only JPEG, PNG, GIF, and WebP images are allowed.");
            return null;
        }

        var uploadsFolder = Path.Combine(env.WebRootPath, "images");
        logger.LogInformation("Uploads folder path: {Path}", uploadsFolder);

        // Ensure the directory exists
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
            logger.LogInformation("Created uploads directory: {Path}", uploadsFolder);
        }

        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        logger.LogInformation("File path: {Path}", filePath);

        try
        {
            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }
            logger.LogInformation("Image saved successfully: {FileName}", uniqueFileName);
            return uniqueFileName;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving image: {Message}", ex.Message);
            ModelState.AddModelError("imageFile", $"Error saving image: {ex.Message}");
            return null;
        }
    }

    // *** FIX: Add a helper to delete an image file ***
    private void DeleteImage(string fileName)
    {
        var filePath = Path.Combine(env.WebRootPath, "images", fileName);
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
            logger.LogInformation("Deleted image file: {Path}", filePath);
        }
    }
    
    // Helper method to save processed image data (base64)
    private async Task<string?> SaveProcessedImage(string base64Image)
    {
        logger.LogInformation("SaveProcessedImage called with base64 data");
        
        try
        {
            // Validate the base64 string
            if (string.IsNullOrEmpty(base64Image) || !base64Image.StartsWith("data:image/"))
            {
                ModelState.AddModelError("processedImageData", "Invalid image data format.");
                return null;
            }
            
            // Extract the actual base64 data (remove the data:image/xxx;base64, prefix)
            var base64Data = base64Image.Substring(base64Image.IndexOf(',') + 1);
            var imageBytes = Convert.FromBase64String(base64Data);
            
            // Check file size (2MB limit)
            if (imageBytes.Length == 0 || imageBytes.Length > 2 * 1024 * 1024)
            {
                ModelState.AddModelError("processedImageData", "File size must be between 1 byte and 2MB.");
                return null;
            }
            
            var uploadsFolder = Path.Combine(env.WebRootPath, "images");
            
            // Ensure the directory exists
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            
            // Generate a unique filename
            var uniqueFileName = Guid.NewGuid().ToString() + ".jpg";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            
            // Save the file
            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
            logger.LogInformation("Processed image saved successfully: {FileName}", uniqueFileName);
            
            return uniqueFileName;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving processed image: {Message}", ex.Message);
            ModelState.AddModelError("processedImageData", $"Error saving processed image: {ex.Message}");
            return null;
        }
    }

    public IActionResult Delete(int id)
    {
        var menuItem = db.MenuItems.Include(m => m.Category).FirstOrDefault(m => m.MenuItemId == id);
        if (menuItem == null) return NotFound();
        return View(menuItem);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        var menuItem = db.MenuItems.Find(id);
        if (menuItem != null)
        {
            db.MenuItems.Remove(menuItem);
            db.SaveChanges();
        }
        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous]
    public IActionResult Details(int id)
    {
        var menuItem = db.MenuItems.Include(m => m.Category)
                                   .Include(m => m.MenuItemImages)
                                   .FirstOrDefault(m => m.MenuItemId == id);
        if (menuItem == null) return NotFound();
        var ratings = db.MenuItemRatings.Where(r => r.MenuItemId == id).ToList();
        var comments = db.MenuItemComments.Include(c => c.Member).Where(c => c.MenuItemId == id).OrderByDescending(c => c.CommentedAt).ToList();
        var vm = new MenuItemDetailsVM
        {
            MenuItem = menuItem,
            Ratings = ratings,
            Comments = comments
        };
        return View(vm);
    }

    [HttpPost]
    public IActionResult AddRating(int menuItemId, int value)
    {
        if (!User.Identity.IsAuthenticated) return Unauthorized();
        var email = User.Identity.Name;
        var existing = db.MenuItemRatings.FirstOrDefault(r => r.MenuItemId == menuItemId && r.MemberEmail == email);
        if (existing != null)
        {
            existing.Value = value;
            existing.RatedAt = DateTime.Now;
        }
        else
        {
            db.MenuItemRatings.Add(new MenuItemRating { MenuItemId = menuItemId, Value = value, MemberEmail = email });
        }
        db.SaveChanges();
        var ratings = db.MenuItemRatings.Where(r => r.MenuItemId == menuItemId).ToList();
        return Json(new { avg = ratings.Count > 0 ? ratings.Average(r => r.Value) : 0, count = ratings.Count });
    }

    [HttpPost]
    public IActionResult AddComment(int menuItemId, string content)
    {
        if (!User.Identity.IsAuthenticated) return Unauthorized();
        var email = User.Identity.Name;
        var comment = new MenuItemComment { MenuItemId = menuItemId, Content = content, MemberEmail = email };
        db.MenuItemComments.Add(comment);
        db.SaveChanges();
        return Json(new { user = email, content, time = comment.CommentedAt });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public IActionResult ToggleActive(int id)
    {
        var menuItem = db.MenuItems.Find(id);
        if (menuItem == null) return NotFound();

        menuItem.IsActive = !menuItem.IsActive;
        db.SaveChanges();

        TempData["SuccessMessage"] = $"Menu item '{menuItem.Name}' is now {(menuItem.IsActive ? "Active" : "Inactive")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Member")]
    public IActionResult ToggleFavorite(int menuItemId)
    {
        if (!User.Identity.IsAuthenticated) return Unauthorized();
        var email = User.Identity.Name;
        var fav = db.MenuItemFavorites.FirstOrDefault(f => f.MenuItemId == menuItemId && f.MemberEmail == email);
        if (fav != null)
        {
            db.MenuItemFavorites.Remove(fav);
            db.SaveChanges();
            return Json(new { favorited = false });
        }
        else
        {
            db.MenuItemFavorites.Add(new MenuItemFavorite { MenuItemId = menuItemId, MemberEmail = email });
            db.SaveChanges();
            return Json(new { favorited = true });
        }
    }

    [HttpGet]
    public IActionResult GetUserFavorites()
    {
        if (!User.Identity.IsAuthenticated) return Unauthorized();
        var email = User.Identity.Name;
        var favorites = db.MenuItemFavorites.Where(f => f.MemberEmail == email)
            .Select(f => f.MenuItemId).ToList();
        return Json(favorites);
    }
    
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetItemDetails(int id)
    {
        var menuItem = db.MenuItems
            .Include(m => m.Category)
            .Where(m => m.MenuItemId == id)
            .Select(m => new {
                id = m.MenuItemId,
                name = m.Name,
                price = m.Price,
                photoURL = m.PhotoURL,
                categoryName = m.Category.Name,
                stockQuantity = m.StockQuantity
            })
            .FirstOrDefault();
            
        if (menuItem == null) return NotFound();
        return Json(menuItem);
    }
    
    [HttpPost]
    [AllowAnonymous]
    public IActionResult GetStockInfo([FromBody] StockInfoRequest request)
    {
        if (request?.ItemIds == null || !request.ItemIds.Any())
            return BadRequest("No item IDs provided");
            
        var stockInfo = db.MenuItems
            .Where(m => request.ItemIds.Contains(m.MenuItemId))
            .Select(m => new {
                id = m.MenuItemId,
                stockQuantity = m.StockQuantity
            })
            .ToList();
            
        return Json(stockInfo);
    }
    
    public class StockInfoRequest
    {
        public List<int> ItemIds { get; set; }
    }

    [HttpGet]
    public IActionResult GetTopSellItems(int count = 5)
    {
        var topItems = db.MenuItems
            .OrderByDescending(m => db.CartItems.Where(c => c.MenuItemId == m.MenuItemId).Count())
            .Take(count)
            .Select(m => new {
                m.MenuItemId,
                m.Name,
                m.PhotoURL,
                m.Price,
                m.Description
            }).ToList();
        return Json(topItems);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult GetFilteredMenuItems(string? search, int? categoryId, decimal? minPrice, decimal? maxPrice)
    {
        try
        {
            var query = db.MenuItems
                .Include(m => m.Category)
                .AsQueryable();

            // Search by name, description, or category name
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => m.Name.Contains(search) || m.Description.Contains(search) || m.Category.Name.Contains(search));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(m => m.CategoryId == categoryId.Value);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(m => m.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(m => m.Price <= maxPrice.Value);
            }

            // Hide inactive items for non-admins
            if (!User.IsInRole("Admin"))
            {
                query = query.Where(m => m.IsActive);
            }

            var items = query
                .OrderBy(m => m.Name)
                .Select(m => new
                {
                    menuItemId = m.MenuItemId,
                    name = m.Name,
                    photoURL = m.PhotoURL,
                    categoryName = m.Category.Name,
                    price = m.Price.ToString("C", new System.Globalization.CultureInfo("en-MY")),
                    isActive = m.IsActive,
                    stockQuantity = m.StockQuantity
                })
                .ToList();

            return Json(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering menu items");
            return BadRequest(new { error = "Failed to filter menu items" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpload(IFormFile txtFile)
    {
        if (txtFile == null || txtFile.Length == 0)
        {
            TempData["BulkUploadMessage"] = "No file selected.";
            return RedirectToAction("Create");
        }

        var categories = await db.Categories.ToListAsync();

        using var reader = new StreamReader(txtFile.OpenReadStream());
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 4) continue;

            string name = parts[0].Trim();
            string description = parts[1].Trim();
            if (!decimal.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price)) continue;
            string categoryName = parts[3].Trim();
            string photoURL = parts.Length >= 5 ? parts[4].Trim() : null;

            var category = categories.FirstOrDefault(c => c.Name == categoryName);
            if (category == null)
            {
                category = new Category { Name = categoryName };
                db.Categories.Add(category);
                await db.SaveChangesAsync();
                categories.Add(category);
            }

            // Try to parse stock quantity if available (6th column), default to 0 if not provided
            int stockQuantity = 0;
            if (parts.Length >= 6 && int.TryParse(parts[5].Trim(), out var parsedStock))
            {
                stockQuantity = parsedStock;
            }

            db.MenuItems.Add(new MenuItem
            {
                Name = name,
                Description = description,
                Price = price,
                CategoryId = category.CategoryId,
                PhotoURL = string.IsNullOrEmpty(photoURL) ? null : photoURL,
                IsActive = true,
                StockQuantity = stockQuantity
            });
        }

        await db.SaveChangesAsync();
        TempData["BulkUploadMessage"] = "Menu items uploaded successfully!";
        return RedirectToAction("Create");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UploadPhotos(List<IFormFile> photos)
    {
        if (photos == null || photos.Count == 0)
        {
            TempData["Message"] = "No files selected.";
            return RedirectToAction("Create");
        }

        var uploadPath = Path.Combine(env.WebRootPath, "images");
        if (!Directory.Exists(uploadPath))
            Directory.CreateDirectory(uploadPath);

        foreach (var photo in photos)
        {
            if (photo.Length > 0)
            {
                var filePath = Path.Combine(uploadPath, Path.GetFileName(photo.FileName));
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    photo.CopyTo(stream);
                }
            }
        }

        TempData["Message"] = "Photos uploaded successfully!";
        return RedirectToAction("Create");
    }


}