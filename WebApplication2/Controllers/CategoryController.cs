using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;
using Microsoft.Extensions.Logging;

namespace WebApplication2.Controllers;

public class CategoryController : Controller
{
    private readonly DB db;
    private readonly ILogger<CategoryController> logger;
    
    public CategoryController(DB db, ILogger<CategoryController> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    // GET: /Category
    public IActionResult Index()
    {
        return View(db.Categories.ToList());
    }

    // GET: /Category/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Category/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Category category)
    {
        logger.LogInformation("Create action called with Category: {@Category}", category);
        
        if (!ModelState.IsValid)
        {
            logger.LogWarning("ModelState is invalid. Errors: {@Errors}", 
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return View(category);
        }

        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError("Name", "Category name is required.");
                return View(category);
            }

            // Normalize/trim name
            category.Name = category.Name.Trim();

            // Check if category name already exists (case-insensitive)
            var existingCategory = db.Categories
                .AsEnumerable()
                .FirstOrDefault(c => string.Equals(c.Name?.Trim(), category.Name, StringComparison.OrdinalIgnoreCase));
            if (existingCategory != null)
            {
                ModelState.AddModelError("Name", "A category with this name already exists.");
                return View(category);
            }

            logger.LogInformation("Adding Category to database: {@Category}", category);
            db.Categories.Add(category);
            var result = db.SaveChanges();
            logger.LogInformation("SaveChanges result: {Result}", result);

            TempData["SuccessMessage"] = $"Category '{category.Name}' was created successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating category: {Message}", ex.Message);
            ModelState.AddModelError("", $"Error creating category: {ex.Message}");
            return View(category);
        }
    }

    // GET: /Category/Edit/5
    public IActionResult Edit(int id)
    {
        var category = db.Categories.Find(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // POST: /Category/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Category category)
    {
        logger.LogInformation("Edit action called with id: {Id}, Category: {@Category}", id, category);
        
        if (id != category.CategoryId) return NotFound();
        
        if (!ModelState.IsValid)
        {
            logger.LogWarning("ModelState is invalid. Errors: {@Errors}", 
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return View(category);
        }

        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError("Name", "Category name is required.");
                return View(category);
            }

            // Normalize/trim name
            category.Name = category.Name.Trim();

            // Check if category name already exists (excluding current category), case-insensitive
            var existingCategory = db.Categories
                .AsEnumerable()
                .FirstOrDefault(c => c.CategoryId != id && string.Equals(c.Name?.Trim(), category.Name, StringComparison.OrdinalIgnoreCase));
            if (existingCategory != null)
            {
                ModelState.AddModelError("Name", "A category with this name already exists.");
                return View(category);
            }

            logger.LogInformation("Updating Category in database: {@Category}", category);
            db.Entry(category).State = EntityState.Modified;
            var result = db.SaveChanges();
            logger.LogInformation("SaveChanges result: {Result}", result);

            TempData["SuccessMessage"] = $"Category '{category.Name}' was updated successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating category: {Message}", ex.Message);
            ModelState.AddModelError("", $"Error updating category: {ex.Message}");
            return View(category);
        }
    }

    // GET: /Category/Delete/5
    public IActionResult Delete(int id)
    {
        var category = db.Categories.Find(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // POST: /Category/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        var category = db.Categories.Find(id);
        if (category != null)
        {
            db.Categories.Remove(category);
            db.SaveChanges();
            TempData["SuccessMessage"] = $"Category '{category.Name}' deleted. Items remain intact and can be reassigned later.";
        }
        return RedirectToAction(nameof(Index));
    }

    // POST: /Category/AssignItems
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignItems(int categoryId, int[] menuItemIds)
    {
        if (categoryId <= 0)
        {
            TempData["ErrorMessage"] = "Invalid category.";
            return RedirectToAction(nameof(Index));
        }
        var category = db.Categories.Find(categoryId);
        if (category == null)
        {
            TempData["ErrorMessage"] = "Category not found.";
            return RedirectToAction(nameof(Index));
        }
        if (menuItemIds == null || menuItemIds.Length == 0)
        {
            TempData["ErrorMessage"] = "No menu items selected.";
            return RedirectToAction("Details", new { id = categoryId });
        }

        var items = db.MenuItems.Where(m => menuItemIds.Contains(m.MenuItemId)).ToList();
        foreach (var item in items)
        {
            item.CategoryId = categoryId;
        }
        db.SaveChanges();
        TempData["SuccessMessage"] = $"Assigned {items.Count} item(s) to '{category.Name}'.";
        return RedirectToAction("Details", new { id = categoryId });
    }

    // POST: /Category/UnassignItems
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UnassignItems(int categoryId, int[] menuItemIds)
    {
        var category = db.Categories.Find(categoryId);
        if (category == null)
        {
            TempData["ErrorMessage"] = "Category not found.";
            return RedirectToAction(nameof(Index));
        }
        if (menuItemIds == null || menuItemIds.Length == 0)
        {
            TempData["ErrorMessage"] = "No menu items selected to unassign.";
            return RedirectToAction("Details", new { id = categoryId });
        }
        var items = db.MenuItems.Where(m => m.CategoryId == categoryId && menuItemIds.Contains(m.MenuItemId)).ToList();
        foreach (var item in items)
        {
            item.CategoryId = null;
        }
        db.SaveChanges();
        TempData["SuccessMessage"] = $"Unassigned {items.Count} item(s) from '{category.Name}'.";
        return RedirectToAction("Details", new { id = categoryId });
    }

    // GET: /Category/Details/5
    public IActionResult Details(int id)
    {
        var category = db.Categories.Find(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // GET: /Category/Unassigned
    [HttpGet]
    public IActionResult Unassigned()
    {
        var items = db.MenuItems
            .Include(m => m.Category)
            .Where(m => m.CategoryId == null)
            .OrderBy(m => m.Name)
            .ToList();
        ViewBag.Categories = db.Categories.OrderBy(c => c.Name).ToList();
        return View(items);
    }

    // POST: /Category/AssignUnassigned
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignUnassigned(int categoryId, int[] menuItemIds)
    {
        return AssignItems(categoryId, menuItemIds);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddPersonalizationOption(int categoryId, string optionName)
    {
        if (!string.IsNullOrWhiteSpace(optionName) && categoryId > 0)
        {
            db.PersonalizationOptions.Add(new PersonalizationOption { CategoryId = categoryId, Name = optionName.Trim() });
            db.SaveChanges();
        }
        return RedirectToAction("Edit", new { id = categoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemovePersonalizationOption(int optionId, int categoryId)
    {
        var opt = db.PersonalizationOptions.FirstOrDefault(o => o.Id == optionId && o.CategoryId == categoryId);
        if (opt != null)
        {
            db.PersonalizationOptions.Remove(opt);
            db.SaveChanges();
        }
        return RedirectToAction("Edit", new { id = categoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditPersonalizationOption(int optionId, int categoryId, string optionName)
    {
        var option = db.PersonalizationOptions.Find(optionId);
        if (option == null || option.CategoryId != categoryId)
        {
            TempData["ErrorMessage"] = "Personalization option not found.";
            return RedirectToAction("Edit", new { id = categoryId });
        }
        if (string.IsNullOrWhiteSpace(optionName))
        {
            TempData["ErrorMessage"] = "Option name cannot be empty.";
            return RedirectToAction("Edit", new { id = categoryId });
        }
        option.Name = optionName.Trim();
        db.SaveChanges();
        TempData["SuccessMessage"] = $"Personalization option updated.";
        return RedirectToAction("Edit", new { id = categoryId });
    }
}
