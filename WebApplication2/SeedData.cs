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

        // Create DB if it doesn't exist
        context.Database.EnsureCreated();

        // Seed Categories
        if (!context.Categories.Any())
        {
            var categories = new Category[]
            {
                new Category { Name = "Western Food" },
                new Category { Name = "Salads" },
                new Category { Name = "Desserts" },
                new Category { Name = "Beverages" },
            };
            context.Categories.AddRange(categories);
            context.SaveChanges();
        }

        // Read categories again from DB to ensure IDs are generated and tracked
        var dbCategories = context.Categories.ToList();

        // Seed MenuItems
        if (!context.MenuItems.Any())
        {
            var menuItems = new MenuItem[]
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

            context.MenuItems.AddRange(menuItems);
            context.SaveChanges();
        }

        // Seed Admin account
        if (!context.Admins.Any())
        {
            var helper = new Helper(
                serviceProvider.GetRequiredService<IWebHostEnvironment>(),
                serviceProvider.GetRequiredService<IHttpContextAccessor>(),
                serviceProvider.GetRequiredService<IConfiguration>());

            context.Admins.Add(new Admin
            {
                Email = "admin@email.com",
                Name = "Admin",
                Hash = helper.HashPassword("123456")
            });

            context.SaveChanges();
        }
    }
}
