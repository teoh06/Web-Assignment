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

namespace WebApplication2.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MenuItemController : Controller
    {
        private readonly DB db;
        private readonly IWebHostEnvironment env;
        private readonly ILogger<MenuItemController> logger;

        // *** FIX: Inject IWebHostEnvironment to handle file paths correctly ***
        public MenuItemController(DB db, IWebHostEnvironment env, ILogger<MenuItemController> logger)
        {
            this.db = db;
            this.env = env;
            this.logger = logger;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            // Add the success message display logic here or in _Layout.cshtml
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }
            var items = db.MenuItems.Include(m => m.Category).OrderBy(m => m.Name).ToList();
            var vm = new MenuItemIndexVM
            {
                MenuItems = items,
                Categories = db.Categories.ToList()
            };
            return View(vm);
        }

        public IActionResult Create()
        {
            ViewBag.Categories = db.Categories.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Price,CategoryId,PhotoURL")] MenuItem menuItem, IFormFile imageFile)
        {
            logger.LogInformation("Create action called with MenuItem: {@MenuItem}", menuItem);
            logger.LogInformation("ImageFile is null: {IsNull}", imageFile == null);

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

                if (imageFile != null)
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
        public async Task<IActionResult> Edit(int id, [Bind("MenuItemId,Name,Description,Price,CategoryId,PhotoURL")] MenuItem menuItem, IFormFile? imageFile, bool removeImage = false)
        {
            logger.LogInformation("Edit action called with id: {Id}, MenuItem: {@MenuItem}", id, menuItem);
            logger.LogInformation("ImageFile is null: {IsNull}, RemoveImage: {RemoveImage}", imageFile == null, removeImage);

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

                // Update text fields
                menuItemToUpdate.Name = menuItem.Name;
                menuItemToUpdate.Description = menuItem.Description;
                menuItemToUpdate.Price = menuItem.Price;
                menuItemToUpdate.CategoryId = menuItem.CategoryId;

                if (removeImage)
                {
                    // Delete old photo if it exists and clear the URL
                    if (!string.IsNullOrEmpty(menuItemToUpdate.PhotoURL))
                    {
                        DeleteImage(menuItemToUpdate.PhotoURL);
                    }
                    menuItemToUpdate.PhotoURL = null;
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

        [AllowAnonymous]
        [HttpGet]
        public IActionResult GetFilteredMenuItems(string? search, int? categoryId, decimal? minPrice, decimal? maxPrice)
        {
            try
            {
                var query = db.MenuItems
                    .Include(m => m.Category)
                    .AsQueryable();

                // Only search by name
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(m => m.Name.Contains(search));
                }

                // You can keep price filters if needed
                if (minPrice.HasValue)
                {
                    query = query.Where(m => m.Price >= minPrice);
                }

                if (maxPrice.HasValue)
                {
                    query = query.Where(m => m.Price <= maxPrice);
                }

                var items = query
                    .OrderBy(m => m.Name)
                    .Select(m => new
                    {
                        menuItemId = m.MenuItemId,
                        name = m.Name,
                        photoURL = m.PhotoURL,
                        categoryName = m.Category.Name,
                        price = m.Price.ToString("C", new System.Globalization.CultureInfo("en-MY"))
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
    }
}