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
                new Category { Name = "Fast Food" },
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
                    CategoryId = categories.Single(c => c.Name == "Fast Food").CategoryId
                },
                new MenuItem
                {
                    Name = "Margherita Pizza",
                    Description = "Stone-baked pizza with mozzarella, tomato, and basil.",
                    Price = 15.50M,
                    PhotoURL = "pizza.jpg",
                    CategoryId = categories.Single(c => c.Name == "Fast Food").CategoryId
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
}
