using Microsoft.AspNetCore.SignalR;
using WebApplication2.Models;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace WebApplication2.Hubs
{
    public class ChatHub : Microsoft.AspNetCore.SignalR.Hub
    {
        private readonly DB _db;

        public ChatHub(DB db)
        {
            _db = db;
        }

        // --- Admin CRUD operations ---

        public async Task AddMenuItem(string name, string description, string price, string category)
        {
            try
            {
                var cat = _db.Categories.FirstOrDefault(c => c.Name == category);
                if (cat == null)
                {
                    await Clients.Caller.SendAsync("MenuItemAdded", $"❌ Category '{category}' not found.");
                    return;
                }
                var item = new MenuItem
                {
                    Name = name,
                    Description = description,
                    Price = decimal.TryParse(price, out var p) ? p : 0,
                    Category = cat
                };
                _db.MenuItems.Add(item);
                await _db.SaveChangesAsync();
                await Clients.Caller.SendAsync("MenuItemAdded", $"✅ Menu item '{name}' added successfully.");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("MenuItemAdded", $"❌ Error adding menu item: {ex.Message}");
            }
        }

        public async Task ModifyMenuItem(string itemName, string field, string newValue)
        {
            try
            {
                var item = _db.MenuItems.FirstOrDefault(m => m.Name == itemName);
                if (item == null)
                {
                    await Clients.Caller.SendAsync("MenuItemModified", $"❌ Menu item '{itemName}' not found.");
                    return;
                }
                switch (field.ToLower())
                {
                    case "name": item.Name = newValue; break;
                    case "description": item.Description = newValue; break;
                    case "price":
                        if (decimal.TryParse(newValue, out var price)) item.Price = price;
                        else { await Clients.Caller.SendAsync("MenuItemModified", $"❌ Invalid price value."); return; }
                        break;
                    default: await Clients.Caller.SendAsync("MenuItemModified", $"❌ Unknown field '{field}'."); return;
                }
                await _db.SaveChangesAsync();
                await Clients.Caller.SendAsync("MenuItemModified", $"✏️ Menu item '{itemName}' updated ({field}).");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("MenuItemModified", $"❌ Error modifying menu item: {ex.Message}");
            }
        }

        public async Task DeleteMenuItem(string name)
        {
            try
            {
                var item = _db.MenuItems.FirstOrDefault(m => m.Name == name);
                if (item == null)
                {
                    await Clients.Caller.SendAsync("MenuItemDeleted", $"❌ Menu item '{name}' not found.");
                    return;
                }
                _db.MenuItems.Remove(item);
                await _db.SaveChangesAsync();
                await Clients.Caller.SendAsync("MenuItemDeleted", $"🗑️ Menu item '{name}' deleted.");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("MenuItemDeleted", $"❌ Error deleting menu item: {ex.Message}");
            }
        }

        public async Task UpdateItemPrice(string itemName, string newPrice)
        {
            try
            {
                var item = _db.MenuItems.FirstOrDefault(m => m.Name == itemName);
                if (item == null)
                {
                    await Clients.Caller.SendAsync("ItemPriceUpdated", $"❌ Menu item '{itemName}' not found.");
                    return;
                }
                if (decimal.TryParse(newPrice, out var price))
                {
                    item.Price = price;
                    await _db.SaveChangesAsync();
                    await Clients.Caller.SendAsync("ItemPriceUpdated", $"💰 Price for '{itemName}' updated to ${newPrice}.");
                }
                else
                {
                    await Clients.Caller.SendAsync("ItemPriceUpdated", $"❌ Invalid price value.");
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ItemPriceUpdated", $"❌ Error updating price: {ex.Message}");
            }
        }

        public async Task ModifyCategory(string currentName, string newName)
        {
            try
            {
                var category = _db.Categories.FirstOrDefault(c => c.Name == currentName);
                if (category == null)
                {
                    await Clients.Caller.SendAsync("CategoryModified", $"❌ Category '{currentName}' not found.");
                    return;
                }
                category.Name = newName;
                await _db.SaveChangesAsync();
                await Clients.Caller.SendAsync("CategoryModified", $"✏️ Category '{currentName}' renamed to '{newName}'.");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("CategoryModified", $"❌ Error modifying category: {ex.Message}");
            }
        }

        public async Task DeleteCategory(string name)
        {
            try
            {
                var category = _db.Categories.FirstOrDefault(c => c.Name == name);
                if (category == null)
                {
                    await Clients.Caller.SendAsync("CategoryDeleted", $"❌ Category '{name}' not found.");
                    return;
                }
                _db.Categories.Remove(category);
                await _db.SaveChangesAsync();
                await Clients.Caller.SendAsync("CategoryDeleted", $"🗑️ Category '{name}' deleted.");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("CategoryDeleted", $"❌ Error deleting category: {ex.Message}");
            }
        }
    }
}