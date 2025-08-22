using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebApplication2.Models;

namespace WebApplication2
{
    public class ChatHub : Hub
    {
        private readonly DB _db;

        public ChatHub(DB db)
        {
            _db = db;
        }

        public async Task ProcessUserMessage(string userRole, string userIdentifier, string message)
        {
            try
            {
                // Process message based on user role
                switch (userRole)
                {
                    case "Guest":
                        await ProcessGuestMessage(message);
                        break;
                    case "Member":
                        await ProcessMemberMessage(userIdentifier, message);
                        break;
                    case "Admin":
                        await ProcessAdminMessage(userIdentifier, message);
                        break;
                    default:
                        await ProcessGuestMessage(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveResponse", $"Sorry, an error occurred: {ex.Message}");
            }
        }

        private async Task ProcessGuestMessage(string message)
        {
            // Check if message contains order request
            if (ContainsOrderRequest(message))
            {
                await Clients.Caller.SendAsync("ReceiveResponse", "Please login to access ordering features.");
                await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Create account", "Login", "Menu recommendations", "Delivery options" });
                return;
            }

            // Check if message contains admin request
            if (ContainsAdminRequest(message))
            {
                await Clients.Caller.SendAsync("ReceiveResponse", "Please login as an admin to access this feature.");
                await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Login", "Menu recommendations", "Delivery options", "Payment methods" });
                return;
            }

            // General response for other queries
            string response = GetGeneralResponse(message);
            await Clients.Caller.SendAsync("ReceiveResponse", response);

            // Show appropriate suggestions
            await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Menu recommendations", "Create account", "Delivery options", "Payment methods" });
        }

        private async Task ProcessMemberMessage(string userIdentifier, string message)
        {
            // Check if message contains order request
            if (ContainsOrderRequest(message))
            {
                // Extract order details
                var orderItems = ExtractOrderItems(message);

                if (orderItems.Count > 0)
                {
                    // Prepare cart items
                    var cartItems = new List<object>();

                    foreach (var item in orderItems)
                    {
                        // Find menu item in database - exact match first, then partial match
                        var menuItem = _db.MenuItems.FirstOrDefault(m => 
                            m.Name.ToLower() == item.Name.ToLower());
                            
                        // If no exact match, try partial match
                        if (menuItem == null)
                        {
                            menuItem = _db.MenuItems.FirstOrDefault(m => 
                                m.Name.ToLower().Contains(item.Name.ToLower()) || 
                                item.Name.ToLower().Contains(m.Name.ToLower()));
                        }

                        if (menuItem != null)
                        {
                            cartItems.Add(new
                            {
                                menuItemId = menuItem.MenuItemId,
                                name = menuItem.Name,
                                quantity = item.Quantity,
                                selectedPersonalizations = ""
                            });
                        }
                    }

                    if (cartItems.Count > 0)
                    {
                        // Calculate total quantity
                        int totalQuantity = orderItems.Sum(x => x.Quantity);
                        // Send items to client to add to cart
                        await Clients.Caller.SendAsync("AddToCart", cartItems);
                        
                        // Confirm order
                        await Clients.Caller.SendAsync("ReceiveResponse", $"I've added {totalQuantity} item(s) to your cart. Would you like to add anything else? Click 'View cart' to see your items or 'Checkout' to proceed with your order.");
                        await Clients.Caller.SendAsync("ShowSuggestions", new[] { "View cart", "Checkout", "Add more items", "Menu recommendations" });
                        return;
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveResponse", "I couldn't find the items you mentioned in our menu. Could you please specify the items more clearly?");
                        await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Show menu", "Menu recommendations", "Order help", "Contact support" });
                        return;
                    }
                }
            }

            // Check if message contains admin request
            if (ContainsAdminRequest(message))
            {
                await Clients.Caller.SendAsync("ReceiveResponse", "You need admin privileges to perform this action.");
                await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Order food", "Track my order", "Menu recommendations", "Delivery options" });
                return;
            }

            // General response for other queries
            string response = GetGeneralResponse(message);
            await Clients.Caller.SendAsync("ReceiveResponse", response);

            // Show appropriate suggestions
            await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Order food", "Track my order", "Menu recommendations", "Delivery options" });
        }

        private async Task ProcessAdminMessage(string userIdentifier, string message)
        {
            // Check if message contains price modification request
            if (ContainsAdminRequest(message))
            {
                var (action, itemName, newPrice) = ExtractPriceModificationDetails(message);

                if (!string.IsNullOrEmpty(itemName) && newPrice > 0)
                {
                    // Find menu item in database - exact match first, then partial match
                    var menuItem = _db.MenuItems.FirstOrDefault(m => 
                        m.Name.ToLower() == itemName.ToLower());
                        
                    // If no exact match, try partial match
                    if (menuItem == null)
                    {
                        menuItem = _db.MenuItems.FirstOrDefault(m => 
                            m.Name.ToLower().Contains(itemName.ToLower()) || 
                            itemName.ToLower().Contains(m.Name.ToLower()));
                    }

                    if (menuItem != null)
                    {
                        // Send confirmation request
                        await Clients.Caller.SendAsync("ConfirmAdminAction", 
                            $"Do you really want to proceed with modifying the price of {menuItem.Name} from RM {menuItem.Price:F2} to RM {newPrice:F2}?",
                            new { action = "ModifyPrice", itemName = menuItem.Name, newPrice = newPrice });
                        return;
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveResponse", $"I couldn't find a menu item named '{itemName}'. Please check the name and try again.");
                        await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Show menu items", "Modify another price", "Menu management", "Order statistics" });
                        return;
                    }
                }
            }

            // General response for other queries
            string response = GetGeneralResponse(message);
            await Clients.Caller.SendAsync("ReceiveResponse", response);

            // Show appropriate suggestions
            await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Modify prices", "Menu management", "Order statistics", "Customer feedback" });
        }

        public async Task ConfirmAdminAction(string action, string itemName, decimal newPrice)
        {
            try
            {
                if (action == "ModifyPrice")
                {
                    // Find menu item in database
                    var menuItem = _db.MenuItems.FirstOrDefault(m => m.Name == itemName);

                    if (menuItem != null)
                    {
                        // Update price
                        menuItem.Price = newPrice;
                        _db.SaveChanges();

                        await Clients.Caller.SendAsync("ReceiveResponse", $"Price of {itemName} has been successfully updated to RM {newPrice:F2}.");
                        await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Modify another price", "Menu management", "Order statistics", "Customer feedback" });
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveResponse", $"Error: Menu item '{itemName}' not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveResponse", $"Error updating price: {ex.Message}");
            }
        }

        public async Task ProcessImageUpload(string userRole, string userIdentifier, string imageUrl)
        {
            try
            {
                // In a real implementation, this would call an image recognition API
                // For demo purposes, we'll simulate recognition results
                var recognizedItems = SimulateImageRecognition(imageUrl);

                if (recognizedItems.Count > 0)
                {
                    // Send recognition results to client
                    await Clients.Caller.SendAsync("ShowImageRecognitionResults", new { items = recognizedItems });

                    // If guest, remind about login for ordering
                    if (userRole == "Guest")
                    {
                        await Clients.Caller.SendAsync("ReceiveResponse", "Please login to order these items.");
                    }
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveResponse", "I couldn't recognize any food items in this image. Could you try another image or describe what you're looking for?");
                    await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Menu recommendations", "Upload another image", "Search by name", "Contact support" });
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveResponse", $"Sorry, there was an error processing your image: {ex.Message}");
            }
        }

        #region Helper Methods

        private bool ContainsOrderRequest(string message)
        {
            string lowerMessage = message.ToLower();
            return lowerMessage.Contains("order") || 
                   lowerMessage.Contains("add to cart") || 
                   lowerMessage.Contains("add to my cart") || 
                   lowerMessage.Contains("buy") || 
                   lowerMessage.Contains("purchase") || 
                   (lowerMessage.Contains("want") && (lowerMessage.Contains("get") || lowerMessage.Contains("have")));
        }

        private bool ContainsAdminRequest(string message)
        {
            string lowerMessage = message.ToLower();
            return lowerMessage.Contains("modify price") || 
                   lowerMessage.Contains("change price") || 
                   lowerMessage.Contains("update price") || 
                   lowerMessage.Contains("set price") || 
                   (lowerMessage.Contains("price") && lowerMessage.Contains("to rm"));
        }

        private (string action, string itemName, decimal newPrice) ExtractPriceModificationDetails(string message)
        {
            string action = "ModifyPrice";
            string itemName = "";
            decimal newPrice = 0;

            // Extract item name and price using regex
            // Pattern for "Modify the price of [item] to RM [price]"
            var pattern1 = new Regex(@"(?:modify|change|update|set)\s+(?:the\s+)?price\s+of\s+([\w\s]+)\s+to\s+(?:RM|rm)\s+([\d\.]+)", RegexOptions.IgnoreCase);
            var match1 = pattern1.Match(message);

            if (match1.Success)
            {
                itemName = match1.Groups[1].Value.Trim();
                decimal.TryParse(match1.Groups[2].Value, out newPrice);
                return (action, itemName, newPrice);
            }

            // Pattern for "[item] price to RM [price]"
            var pattern2 = new Regex(@"([\w\s]+)\s+price\s+to\s+(?:RM|rm)\s+([\d\.]+)", RegexOptions.IgnoreCase);
            var match2 = pattern2.Match(message);

            if (match2.Success)
            {
                itemName = match2.Groups[1].Value.Trim();
                decimal.TryParse(match2.Groups[2].Value, out newPrice);
            }

            return (action, itemName, newPrice);
        }

        private List<(string Name, int Quantity)> ExtractOrderItems(string message)
        {
            var items = new List<(string Name, int Quantity)>();
            string lowerMessage = message.ToLower();

            // Map for word-based numbers
            var numberWords = new Dictionary<string, int>
            {
                {"one", 1}, {"two", 2}, {"three", 3}, {"four", 4}, {"five", 5},
                {"six", 6}, {"seven", 7}, {"eight", 8}, {"nine", 9}, {"ten", 10},
                {"eleven", 11}, {"twelve", 12}
            };

            // Pattern for numeric quantity: "3 caesar salad"
            var pattern1 = new Regex(@"(?:order|get|add|buy|want)?\s*(\d+)\s+([\w\s]+?)(?:\s+to\s+(?:my\s+)?cart|\s*$)", RegexOptions.IgnoreCase);
            var matches1 = pattern1.Matches(lowerMessage);
            foreach (Match match in matches1)
            {
                int quantity;
                if (int.TryParse(match.Groups[1].Value, out quantity))
                {
                    string itemName = match.Groups[2].Value.Trim();
                    items.Add((itemName, quantity));
                }
            }

            // Pattern for word-based quantity: "three caesar salad"
            var pattern2 = new Regex(@"(?:order|get|add|buy|want)?\s*(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\s+([\w\s]+?)(?:\s+to\s+(?:my\s+)?cart|\s*$)", RegexOptions.IgnoreCase);
            var matches2 = pattern2.Matches(lowerMessage);
            foreach (Match match in matches2)
            {
                string word = match.Groups[1].Value.Trim();
                int quantity = numberWords.ContainsKey(word) ? numberWords[word] : 1;
                string itemName = match.Groups[2].Value.Trim();
                items.Add((itemName, quantity));
            }

            // Pattern for "[item]" without explicit quantity
            if (items.Count == 0)
            {
                var pattern3 = new Regex(@"(?:order|get|add|buy|want)\s+([\w\s]+?)(?:\s+to\s+(?:my\s+)?cart|\s*$)", RegexOptions.IgnoreCase);
                var matches3 = pattern3.Matches(lowerMessage);
                foreach (Match match in matches3)
                {
                    string itemName = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(itemName) && !itemName.Contains("order") && !itemName.Contains("cart"))
                    {
                        items.Add((itemName, 1)); // Default quantity is 1
                    }
                }
            }

            // Check for specific menu items if no matches found
            if (items.Count == 0)
            {
                // Check for our actual menu items
                string[] menuItems = new[] { "Classic Burger", "Margherita Pizza", "Caesar Salad", "Tiramisu", "Coca-Cola", "Fish and Chip", "Pudding", "Iced Latte" };
                foreach (var item in menuItems)
                {
                    if (lowerMessage.Contains(item.ToLower()))
                    {
                        items.Add((item, 1)); // Default quantity is 1
                    }
                }
            }

            return items;
        }

        private string GetGeneralResponse(string message)
        {
            string lowerMessage = message.ToLower();

            // Menu recommendations
            if (lowerMessage.Contains("menu") || lowerMessage.Contains("recommend") || lowerMessage.Contains("special"))
            {
                // Add direct link to menu (corrected to /MenuItem)
                return "Our current specials include Classic Burger, Margherita Pizza, and Fish and Chip. <a href='/MenuItem' target='_blank' style='color:#2196F3;font-weight:bold;'>Browse our full menu</a> or let me know if you'd like to place an order.";
            }

            // Delivery options
            if (lowerMessage.Contains("delivery") || lowerMessage.Contains("shipping") || lowerMessage.Contains("bring"))
            {
                // Add direct link to cart for delivery options
                return "We offer delivery within a 10km radius. Delivery is free for orders over RM50, otherwise there's a RM5 delivery fee. Estimated delivery time is 30-45 minutes depending on your location. <a href='/Cart' target='_blank' style='color:#2196F3;font-weight:bold;'>View your cart</a> when you're ready to checkout and select your delivery options.";
            }

            // Payment methods
            if (lowerMessage.Contains("payment") || lowerMessage.Contains("pay") || lowerMessage.Contains("credit") || lowerMessage.Contains("card"))
            {
                // Add direct link to checkout
                return "We accept all major credit cards, online banking, and cash on delivery. You can securely save your payment method for faster checkout next time. <a href='/Cart/Payment' target='_blank' style='color:#2196F3;font-weight:bold;'>Go to checkout</a> when you're ready to complete your order.";
            }

            // Track order
            if (lowerMessage.Contains("track") || lowerMessage.Contains("where") || lowerMessage.Contains("status") || lowerMessage.Contains("my order"))
            {
                // Add direct link to tracking page
                return "You can track your order in real-time from our tracking page. <a href='/Cart/Track' target='_blank' style='color:#2196F3;font-weight:bold;'>Track your order</a> or <a href='/Cart/History' target='_blank' style='color:#2196F3;font-weight:bold;'>view your order history</a>.";
            }

            // View cart
            if (lowerMessage.Contains("cart") || lowerMessage.Contains("basket") || lowerMessage.Contains("my items"))
            {
                // Add direct link to cart
                return "You can view your current cart items, modify quantities, or proceed to checkout. <a href='/Cart' target='_blank' style='color:#2196F3;font-weight:bold;'>View your cart</a> to manage your order.";
            }

            // Default response
            return "How can I assist you with your food order today? You can ask about our menu, place an order, or inquire about delivery options. Click on any suggestion below to get started.";
        }

        private List<object> SimulateImageRecognition(string imageUrl)
        {
            // In a real implementation, this would call an image recognition API
            // For demo purposes, we'll return items from our actual menu in SeedData.cs
            var possibleItems = new List<object>
            {
                new { name = "Classic Burger", confidence = 0.92 },
                new { name = "Margherita Pizza", confidence = 0.89 },
                new { name = "Caesar Salad", confidence = 0.85 },
                new { name = "Tiramisu", confidence = 0.95 },
                new { name = "Coca-Cola", confidence = 0.78 },
                new { name = "Fish and Chip", confidence = 0.82 },
                new { name = "Pudding", confidence = 0.87 },
                new { name = "Iced Latte", confidence = 0.91 }
            };

            // Randomly select 2-3 items from our actual menu
            var random = new Random();
            int count = random.Next(2, 4);
            var selectedItems = new List<object>();

            for (int i = 0; i < count; i++)
            {
                int index = random.Next(possibleItems.Count);
                selectedItems.Add(possibleItems[index]);
                possibleItems.RemoveAt(index);

                if (possibleItems.Count == 0)
                    break;
            }

            return selectedItems;
        }

        #endregion
    }
}