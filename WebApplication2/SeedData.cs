using WebApplication2.Models;
using Microsoft.EntityFrameworkCore;

namespace WebApplication2.Data;

public static class SeedData
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DB>();
            context.Database.EnsureCreated();

            if (context.Categories.Any())
            {
                return; // Already seeded
            }

            var categories = new Category[]
            {
                new Category { Name = "Burgers" },
                new Category { Name = "Pizzas" },
                new Category { Name = "Salads" },
                new Category { Name = "Desserts" },
            };
            context.Categories.AddRange(categories);
            context.SaveChanges();

            var menuItems = new MenuItem[]
            {
                new MenuItem
                {
                    Name = "Classic Burger",
                    Description = "Juicy beef patty, cheese, lettuce, tomato, and our special sauce.",
                    Price = 12.99M,
                    PhotoURL = "burger.jpg",
                    CategoryId = categories.Single(c => c.Name == "Burgers").CategoryId
                },
                new MenuItem
                {
                    Name = "Margherita Pizza",
                    Description = "Stone-baked pizza with mozzarella, tomato, and basil.",
                    Price = 15.50M,
                    PhotoURL = "pizza.jpg",
                    CategoryId = categories.Single(c => c.Name == "Pizzas").CategoryId
                },
                new MenuItem
                {
                    Name = "Caesar Salad",
                    Description = "Crisp romaine, parmesan, croutons, and Caesar dressing.",
                    Price = 9.75M,
                    PhotoURL = "salad.jpg",
                    CategoryId = categories.Single(c => c.Name == "Salads").CategoryId
                },
                new MenuItem
                {
                    Name = "Tiramisu",
                    Description = "layers of coffee-soaked ladyfinger biscuits, creamy mascarpone cheese, and cocoa powder.",
                    Price = 10.99M,
                    PhotoURL = "tiramisu.jpg",
                    CategoryId = categories.Single(c => c.Name == "Desserts").CategoryId
                },
            };
            context.MenuItems.AddRange(menuItems);
            context.SaveChanges();
        }
    }
}
