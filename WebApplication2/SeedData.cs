using WebApplication2;
using WebApplication2.Models;
using Microsoft.EntityFrameworkCore;

namespace WebApplication2.Data;

public static class SeedData
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DB>();
        context.Database.EnsureCreated();

        SeedAdmins(context, serviceProvider);
        SeedMembers(context, serviceProvider);
        SeedCategories(context);
        SeedMenuItems(context);
        SeedPersonalizationOptions(context);
    }

    // --- Seed Admins ---
    private static void SeedAdmins(DB context, IServiceProvider serviceProvider)
    {
        if (context.Admins.Any()) return;
        var helper = new Helper(
            serviceProvider.GetRequiredService<IWebHostEnvironment>(),
            serviceProvider.GetRequiredService<IHttpContextAccessor>(),
            serviceProvider.GetRequiredService<IConfiguration>());
        var defaultAdmins = new[]
        {
            new Admin
            {
                Email = "admin@gmail.com",
                Name = "Admin",
                Hash = helper.HashPassword("123456")
            }
        };
        context.Admins.AddRange(defaultAdmins);
        context.SaveChanges();
    }

    // --- Seed Members ---
    private static void SeedMembers(DB context, IServiceProvider serviceProvider)
    {
        if (context.Members.Any()) return;
        var helper = new Helper(
            serviceProvider.GetRequiredService<IWebHostEnvironment>(),
            serviceProvider.GetRequiredService<IHttpContextAccessor>(),
            serviceProvider.GetRequiredService<IConfiguration>());
        var defaultMembers = new[]
        {
            new Member
            {
                Email = "yaphb-wm24@student.tarc.edu.my",
                Name = "TARC Student",
                Hash = helper.HashPassword("123456"),
                Address = "TARC University College, Setapak",
                PhoneNumber = "0123456789",
                PhotoURL = "default.png",  // Set default photo
                OtpCode = null,            // Ensure no pending OTP
                OtpExpiry = null,          // Ensure no OTP expiry
                IsPendingDeletion = false, // Not pending deletion
                DeletionRequestDate = null,
                DeletionToken = null,
                MemberPhotos = new List<MemberPhoto>(), // Initialize empty photo collection
                Orders = new List<Order>() // Initialize empty orders collection
            }
        };
        context.Members.AddRange(defaultMembers);
        context.SaveChanges();
    }

    // --- Seed Categories ---
    private static void SeedCategories(DB context)
    {
        if (context.Categories.Any()) return;
        var categories = new[]
        {
            new Category { Name = "Western Food" },
            new Category { Name = "Salads" },
            new Category { Name = "Desserts" },
            new Category { Name = "Beverages" },
        };
        context.Categories.AddRange(categories);
        context.SaveChanges();
    }

    // --- Seed MenuItems ---
    private static void SeedMenuItems(DB context)
    {
        if (context.MenuItems.Any()) return;
        var dbCategories = context.Categories.ToList();
        var rand = new Random();
        var menuItems = new[]
        {
            new MenuItem
            {
                Name = "Classic Burger",
                Description = "Juicy beef patty, cheese, lettuce, tomato, and our special sauce.",
                Price = 12.99M,
                PhotoURL = "burger.jpg",
                CategoryId = dbCategories.Single(c => c.Name == "Western Food").CategoryId
            },
            new MenuItem
            {
                Name = "Margherita Pizza",
                Description = "Stone-baked pizza with mozzarella, tomato, and basil.",
                Price = 15.50M,
                PhotoURL = "pizza.jpg",
                CategoryId = dbCategories.Single(c => c.Name == "Western Food").CategoryId
            },
            new MenuItem
            {
                Name = "Caesar Salad",
                Description = "Crisp romaine, parmesan, croutons, and Caesar dressing.",
                Price = 9.75M,
                PhotoURL = "salad.jpg",
                CategoryId = dbCategories.Single(c => c.Name == "Salads").CategoryId
            },
            new MenuItem
            {
                Name = "Tiramisu",
                Description = "Layers of coffee-soaked ladyfinger biscuits, creamy mascarpone cheese, and cocoa powder.",
                Price = 10.99M,
                PhotoURL = "tiramisu.jpg",
                CategoryId = dbCategories.Single(c => c.Name == "Desserts").CategoryId
            },
            new MenuItem
            {
                Name = "Coca-Cola",
                Description = "Carbonated soft drink with a cola flavor.",
                Price = 2.99M,
                PhotoURL = "cocacola.jpg",
                CategoryId = dbCategories.Single(c => c.Name == "Beverages").CategoryId
            },
            new MenuItem
            {
                Name = "Fish and Chip",
                Description = "Crispy battered white fish, typically cod or haddock, served with thick-cut fries.",
                Price = 21.90M,
                PhotoURL = "fish and chip.jpg",
                CategoryId = dbCategories.Single(c => c.Name == "Western Food").CategoryId
            },
            new MenuItem
            {
                Name = "Pudding",
                Description = "Soft, creamy dessert made from milk, sugar, and a thickening agent like cornstarch or eggs.",
                Price = 8.50M,
                PhotoURL = "pudding.jpg",
                CategoryId = dbCategories.Single(c => c.Name == "Desserts").CategoryId
            },
            new MenuItem
            {
                Name = "Iced Latte",
                Description = "Chilled espresso with milk and ice, perfect for a refreshing pick-me-up.",
                Price = 4.50M,
                PhotoURL = "iced latte.jpg",
                CategoryId = dbCategories.Single(c => c.Name == "Beverages").CategoryId
            }
        };
        foreach (var item in menuItems)
        {
            item.StockQuantity = rand.Next(50, 61);
        }
        context.MenuItems.AddRange(menuItems);
        context.SaveChanges();
    }

    // --- Seed PersonalizationOptions ---
    private static void SeedPersonalizationOptions(DB context)
    {
        if (context.PersonalizationOptions.Any()) return;
        var dbCategories = context.Categories.ToList();
        var westernFoodId = dbCategories.Single(c => c.Name == "Western Food").CategoryId;
        var saladsId = dbCategories.Single(c => c.Name == "Salads").CategoryId;
        var dessertsId = dbCategories.Single(c => c.Name == "Desserts").CategoryId;
        var beveragesId = dbCategories.Single(c => c.Name == "Beverages").CategoryId;
        var options = new[]
        {
            // Western Food
            new PersonalizationOption { CategoryId = westernFoodId, Name = "Extra Cheese" },
            new PersonalizationOption { CategoryId = westernFoodId, Name = "No Onion" },
            new PersonalizationOption { CategoryId = westernFoodId, Name = "Gluten Free Bun" },
            // Salads
            new PersonalizationOption { CategoryId = saladsId, Name = "No Croutons" },
            new PersonalizationOption { CategoryId = saladsId, Name = "Extra Dressing" },
            // Desserts
            new PersonalizationOption { CategoryId = dessertsId, Name = "Extra Cocoa" },
            new PersonalizationOption { CategoryId = dessertsId, Name = "No Nuts" },
            // Beverages
            new PersonalizationOption { CategoryId = beveragesId, Name = "Less Ice" },
            new PersonalizationOption { CategoryId = beveragesId, Name = "No Sugar" },
            new PersonalizationOption { CategoryId = beveragesId, Name = "Soy Milk" }
        };
        context.PersonalizationOptions.AddRange(options);
        context.SaveChanges();
    }
}
