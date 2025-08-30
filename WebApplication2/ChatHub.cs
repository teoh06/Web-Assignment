using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebApplication2.Models;
using System.Net.Http; 
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace WebApplication2
{
    public class ChatHub : Hub
    {
        private readonly DB _db;
        private readonly IConfiguration _config;
        private static readonly HttpClient _httpClient = new HttpClient();

        public ChatHub(DB db, IConfiguration config)
        {
            _db = db;
            _config = config;
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
            // Check for interactive messages first
            var interactiveResponse = GetInteractiveResponse(message, "Guest");
            if (!string.IsNullOrEmpty(interactiveResponse))
            {
                await Clients.Caller.SendAsync("ReceiveResponse", interactiveResponse);
                await Clients.Caller.SendAsync("ShowSuggestions", GetInteractiveSuggestions(message, "Guest"));
                return;
            }

            // Check if message contains order request
            if (ContainsOrderRequest(message))
            {
                await Clients.Caller.SendAsync("ReceiveResponse", "I'd love to help you order! However, you'll need to create an account or login first to access our ordering features. It's quick and free!");
                await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Create account", "Login", "Menu recommendations", "Delivery options" });
                return;
            }

            // Check if message contains admin request
            if (ContainsAdminRequest(message))
            {
                await Clients.Caller.SendAsync("ReceiveResponse", "Administrative features require admin access. Please login as an admin to access this feature.");
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
            // Check for interactive messages first
            var interactiveResponse = GetInteractiveResponse(message, "Member");
            if (!string.IsNullOrEmpty(interactiveResponse))
            {
                await Clients.Caller.SendAsync("ReceiveResponse", interactiveResponse);
                await Clients.Caller.SendAsync("ShowSuggestions", GetInteractiveSuggestions(message, "Member"));
                return;
            }

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

                        // Confirm order with personalized message
                        string confirmationMessage = totalQuantity == 1
                            ? $"Perfect! I've added {orderItems[0].Name} to your cart. Anything else to make your meal complete?"
                            : $"Great choice! I've added {totalQuantity} delicious items to your cart. Ready to satisfy that craving!";

                        await Clients.Caller.SendAsync("ReceiveResponse", confirmationMessage);
                        await Clients.Caller.SendAsync("ShowSuggestions", new[] { "View cart", "Checkout", "Add more items", "Menu recommendations" });
                        return;
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveResponse", "Hmm, I couldn't find those specific items on our menu. Could you help me by being more specific? For example, try 'Classic Burger' or 'Margherita Pizza'.");
                        await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Show menu", "Menu recommendations", "Order help", "Contact support" });
                        return;
                    }
                }
            }

            // Check if message contains admin request
            if (ContainsAdminRequest(message))
            {
                await Clients.Caller.SendAsync("ReceiveResponse", "I appreciate your interest in helping manage QuickBite! However, you'll need admin privileges for that action. Contact our support team if you believe you should have admin access.");
                await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Order food", "Track my order", "Menu recommendations", "Contact support" });
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
            // Check for interactive messages first
            var interactiveResponse = GetInteractiveResponse(message, "Admin");
            if (!string.IsNullOrEmpty(interactiveResponse))
            {
                await Clients.Caller.SendAsync("ReceiveResponse", interactiveResponse);
                await Clients.Caller.SendAsync("ShowSuggestions", GetInteractiveSuggestions(message, "Admin"));
                return;
            }

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
                            $"Admin Action Required: Change {menuItem.Name} price from RM {menuItem.Price:F2} to RM {newPrice:F2}? This will affect all future orders.",
                            new { action = "ModifyPrice", itemName = menuItem.Name, newPrice = newPrice });
                        return;
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveResponse", $"I couldn't locate '{itemName}' in our menu system. Please double-check the item name or try browsing the menu first.");
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
                        var oldPrice = menuItem.Price;
                        menuItem.Price = newPrice;
                        _db.SaveChanges();

                        await Clients.Caller.SendAsync("ReceiveResponse", $"Success! {itemName} price updated from RM {oldPrice:F2} to RM {newPrice:F2}. The change is now live for all customers.");
                        await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Modify another price", "Menu management", "Order statistics", "Customer feedback" });
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveResponse", $"Error: Unable to find menu item '{itemName}' in the database. Please try again.");
                    }
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveResponse", $"Oops! Something went wrong while updating the price: {ex.Message}. Please try again or contact technical support.");
            }
        }

        public async Task ProcessImageUpload(string userRole, string userIdentifier, string imageUrl)
        {
            try
            {
                // Use Azure Computer Vision to get tags/labels
                var tags = await GetImageTagsFromAzure(imageUrl);
                var recognizedItems = RecognizeMenuItemsFromTags(tags);

                if (recognizedItems.Count > 0)
                {
                    await Clients.Caller.SendAsync("ShowImageRecognitionResults", new { items = recognizedItems });
                    if (userRole == "Guest")
                    {
                        await Clients.Caller.SendAsync("ReceiveResponse", "Great photo! I can see some delicious items there. Please create an account or login to order these tasty treats!");
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveResponse", "Wow, that looks delicious! I've identified some menu items from your photo. Click on any item above to add it to your cart!");
                    }
                }
                else
                {
                    string reply = tags.Count > 0
                        ? $"I can see '{string.Join(", ", tags)}' in your image, but I couldn't match them to our current menu items. Would you like to <a href='/MenuItem' target='_blank'>browse our full menu</a> instead?"
                        : "I'm having trouble identifying food items in this image. Could you try uploading a clearer photo or tell me what you're craving instead?";
                    await Clients.Caller.SendAsync("ReceiveResponse", reply);
                    await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Menu recommendations", "Upload another image", "Search by name", "Contact support" });
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveResponse", $"Oops! I encountered an issue processing your image: {ex.Message}. Please try again or describe what you're looking for instead.");
            }
        }

        // --- Interactive Response Handler ---
        private string GetInteractiveResponse(string message, string userRole)
        {
            string lowerMessage = message.ToLower().Trim();

            // Greetings
            if (ContainsGreeting(lowerMessage))
            {
                var greetings = new[]
                {
                    $"Hello there! Welcome to QuickBite! I'm here to help make your dining experience amazing.",
                    $"Hi! Great to see you at QuickBite today! What can I help you discover?",
                    $"Hey! Welcome to QuickBite - where every bite is a delight! How can I assist you?",
                    $"Hello! Thanks for choosing QuickBite! I'm excited to help you find something delicious."
                };
                return greetings[new Random().Next(greetings.Length)];
            }

            // Farewells
            if (ContainsFarewell(lowerMessage))
            {
                var farewells = new[]
                {
                    "Thank you for visiting QuickBite! We hope to serve you again soon. Have a wonderful day!",
                    "Goodbye! It was great helping you today. Come back anytime you're craving something delicious!",
                    "Thanks for choosing QuickBite! Take care and we'll see you next time for more tasty treats!",
                    "Farewell! Remember, we're always here when you need a quick, delicious bite. Until next time!",
                    "See you later! Thanks for being part of the QuickBite family. Have an amazing day ahead!"
                };
                return farewells[new Random().Next(farewells.Length)];
            }

            // Thank you responses
            if (ContainsThanks(lowerMessage))
            {
                var thankResponses = new[]
                {
                    "You're absolutely welcome! That's what I'm here for. Anything else I can help you with?",
                    "My pleasure! I'm always happy to help make your QuickBite experience perfect.",
                    "No problem at all! Is there anything else you'd like to know about our menu or services?",
                    "You're very welcome! I love helping customers discover great food. What else can I do for you?",
                    "Happy to help! That's what good service is all about. Anything else on your mind?"
                };
                return thankResponses[new Random().Next(thankResponses.Length)];
            }

            // Compliments about food/service
            if (ContainsCompliment(lowerMessage))
            {
                var complimentResponses = new[]
                {
                    "That's wonderful to hear! We work hard to make every meal special. Your feedback means the world to us!",
                    "Thank you so much! Our chefs and team will be thrilled to hear that. We're always striving for excellence!",
                    "I'm so glad you enjoyed it! That's exactly what we aim for - creating memorable dining experiences.",
                    "Your kind words just made my day! We're passionate about quality food and service. Thank you!",
                    "That's fantastic feedback! We'll make sure the team knows how much you appreciated their work."
                };
                return complimentResponses[new Random().Next(complimentResponses.Length)];
            }

            // Complaints or issues
            if (ContainsComplaint(lowerMessage))
            {
                var complaintResponses = new[]
                {
                    "I sincerely apologize for that experience. Your satisfaction is our top priority, and I want to make this right. Can you tell me more details?",
                    "I'm really sorry to hear about this issue. This definitely doesn't meet our standards. Let me help you resolve this immediately.",
                    "That's not the QuickBite experience we want for you. I apologize and I'm here to fix this. What can I do to improve your experience?",
                    "I understand your frustration, and I'm sorry this happened. We value your feedback and want to ensure this doesn't happen again. How can I help?"
                };
                return complaintResponses[new Random().Next(complaintResponses.Length)];
            }

            // Questions about the bot/AI
            if (ContainsAIQuestion(lowerMessage))
            {
                return "I'm QuickBite's AI assistant, designed to help you with orders, menu questions, and support! I'm here 24/7 to make your dining experience as smooth as possible. While I'm pretty smart, I'm always learning to serve you better!";
            }

            // Jokes or humor
            if (ContainsJokeRequest(lowerMessage))
            {
                var jokes = new[]
                {
                    "Why don't eggs tell jokes? Because they'd crack each other up! Speaking of eggs, have you tried our breakfast items?",
                    "What do you call a nosy pepper? Jalape�o business! Just like how we mind our business of making great food!",
                    "Why did the tomato turn red? Because it saw the salad dressing! Speaking of salads, our Caesar Salad is amazing!",
                    "What do you call a fake noodle? An impasta! But our pasta dishes are 100% authentic and delicious!",
                    "Why don't burgers ever get cold? Because they're always between buns! Try our Classic Burger - it's always hot and fresh!"
                };
                return jokes[new Random().Next(jokes.Length)];
            }

            // Help requests
            if (ContainsHelpRequest(lowerMessage))
            {
                var helpResponses = userRole switch
                {
                    "Admin" => "I'm here to help with all your admin needs! I can assist with price modifications, menu management, order statistics, and more. What would you like to do?",
                    "Member" => "I'm here to help make your ordering experience perfect! I can help you place orders, track deliveries, recommend dishes, answer questions about our menu, and more. What can I help you with?",
                    _ => "I'm here to help you discover QuickBite! I can show you our menu, explain our delivery options, help with account creation, and answer any questions. What interests you most?"
                };
                return helpResponses;
            }

            // Weather/time related
            if (ContainsWeatherTimeQuery(lowerMessage))
            {
                var weatherResponses = new[]
                {
                    "Whatever the weather, QuickBite has something perfect for you! Rainy day? Try our warm comfort food. Sunny day? Our fresh salads are perfect!",
                    "Any time is a good time for great food! We're open and ready to serve you whenever hunger strikes.",
                    "Perfect weather for food delivery! Our drivers are ready to bring delicious meals right to your door, rain or shine.",
                    "No matter the weather outside, we'll warm your heart with delicious food! What sounds good to you today?"
                };
                return weatherResponses[new Random().Next(weatherResponses.Length)];
            }

            // Confusion or didn't understand
            if (ContainsConfusion(lowerMessage))
            {
                return "I want to make sure I understand exactly what you need! Could you rephrase that or be more specific? I'm here to help and want to get it right for you.";
            }

            return string.Empty; // No interactive response found
        }

        private string[] GetInteractiveSuggestions(string message, string userRole)
        {
            string lowerMessage = message.ToLower().Trim();

            if (ContainsGreeting(lowerMessage))
            {
                return userRole switch
                {
                    "Admin" => new[] { "Menu management", "Order statistics", "Modify prices", "Customer feedback" },
                    "Member" => new[] { "Order food", "Menu recommendations", "Track my order", "View cart" },
                    _ => new[] { "Menu recommendations", "Create account", "Delivery options", "Login" }
                };
            }

            if (ContainsFarewell(lowerMessage))
            {
                return new[] { "Menu recommendations", "Order food", "Contact us", "Visit again" };
            }

            if (ContainsThanks(lowerMessage))
            {
                return userRole switch
                {
                    "Admin" => new[] { "Modify another price", "Order statistics", "Menu management", "Customer feedback" },
                    "Member" => new[] { "Order more food", "Track my order", "Menu recommendations", "View cart" },
                    _ => new[] { "Menu recommendations", "Create account", "Order food", "Delivery options" }
                };
            }

            if (ContainsCompliment(lowerMessage))
            {
                return userRole switch
                {
                    "Member" => new[] { "Order again", "Try new items", "Share feedback", "Recommend to friends" },
                    _ => new[] { "Create account", "Order food", "Menu recommendations", "Delivery options" }
                };
            }

            if (ContainsComplaint(lowerMessage))
            {
                return new[] { "Contact support", "Order refund", "Try again", "Manager assistance" };
            }

            if (ContainsJokeRequest(lowerMessage))
            {
                return userRole switch
                {
                    "Member" => new[] { "Another joke", "Order food", "Menu recommendations", "Fun facts" },
                    _ => new[] { "Another joke", "Menu recommendations", "Create account", "Fun facts" }
                };
            }

            if (ContainsHelpRequest(lowerMessage))
            {
                return userRole switch
                {
                    "Admin" => new[] { "Menu management", "Modify prices", "Order statistics", "Customer support" },
                    "Member" => new[] { "Order food", "Track order", "Menu help", "Account help" },
                    _ => new[] { "Menu recommendations", "Create account", "How to order", "Delivery info" }
                };
            }

            // Default suggestions
            return userRole switch
            {
                "Admin" => new[] { "Menu management", "Order statistics", "Modify prices", "Help" },
                "Member" => new[] { "Order food", "Menu recommendations", "Track order", "Help" },
                _ => new[] { "Menu recommendations", "Create account", "Delivery options", "Help" }
            };
        }

        #region Interactive Message Detection Methods

        private bool ContainsGreeting(string message)
        {
            var greetings = new[] { "hello", "hi", "hey", "good morning", "good afternoon", "good evening", "greetings", "howdy", "sup", "what's up" };
            return greetings.Any(greeting => message.Contains(greeting));
        }

        private bool ContainsFarewell(string message)
        {
            var farewells = new[] { "bye", "goodbye", "see you", "farewell", "take care", "catch you later", "until next time", "gotta go", "leaving", "exit", "quit" };
            return farewells.Any(farewell => message.Contains(farewell));
        }

        private bool ContainsThanks(string message)
        {
            var thanks = new[] { "thank", "thanks", "thx", "appreciate", "grateful", "cheers" };
            return thanks.Any(thank => message.Contains(thank));
        }

        private bool ContainsCompliment(string message)
        {
            var compliments = new[] { "delicious", "amazing", "great", "excellent", "fantastic", "wonderful", "awesome", "perfect", "love it", "best", "incredible", "outstanding", "superb" };
            return compliments.Any(compliment => message.Contains(compliment));
        }

        private bool ContainsComplaint(string message)
        {
            var complaints = new[] { "terrible", "awful", "bad", "horrible", "disgusting", "wrong", "problem", "issue", "complaint", "disappointed", "unsatisfied", "poor", "worst", "hate" };
            return complaints.Any(complaint => message.Contains(complaint));
        }

        private bool ContainsAIQuestion(string message)
        {
            var aiQuestions = new[] { "are you a bot", "are you human", "are you real", "what are you", "who are you", "ai", "artificial", "robot", "chatbot" };
            return aiQuestions.Any(question => message.Contains(question));
        }

        private bool ContainsJokeRequest(string message)
        {
            var jokeRequests = new[] { "joke", "funny", "make me laugh", "humor", "tell me something funny", "entertain me" };
            return jokeRequests.Any(request => message.Contains(request));
        }

        private bool ContainsHelpRequest(string message)
        {
            var helpRequests = new[] { "help", "assist", "support", "guide", "how to", "can you help", "need help", "stuck", "confused", "don't know" };
            return helpRequests.Any(request => message.Contains(request));
        }

        private bool ContainsWeatherTimeQuery(string message)
        {
            var weatherTime = new[] { "weather", "rain", "sunny", "cold", "hot", "time", "today", "now", "currently", "temperature" };
            return weatherTime.Any(query => message.Contains(query));
        }

        private bool ContainsConfusion(string message)
        {
            var confusion = new[] { "what", "huh", "don't understand", "confused", "unclear", "explain", "meaning", "?" };
            return confusion.Any(conf => message.Contains(conf)) || message.Contains("?");
        }

        #endregion

        // --- Azure Computer Vision integration ---
        private async Task<List<string>> GetImageTagsFromAzure(string imageUrl)
        {
            var endpoint = _config["AzureVision:Endpoint"];
            var key = _config["AzureVision:Key"];
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
                return new List<string>();

            var requestUrl = $"{endpoint}/vision/v3.2/analyze?visualFeatures=Tags,Description";
            var requestBody = new { url = imageUrl };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);

            // If imageUrl is base64, use the image binary endpoint
            if (imageUrl.StartsWith("data:image/"))
            {
                var base64Data = imageUrl.Substring(imageUrl.IndexOf(",") + 1);
                var imageBytes = Convert.FromBase64String(base64Data);
                var byteContent = new ByteArrayContent(imageBytes);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var resp = await _httpClient.PostAsync($"{endpoint}/vision/v3.2/analyze?visualFeatures=Tags,Description", byteContent);
                var json = await resp.Content.ReadAsStringAsync();
                return ParseTagsFromAzureResponse(json);
            }
            else
            {
                var resp = await _httpClient.PostAsync(requestUrl, content);
                var json = await resp.Content.ReadAsStringAsync();
                return ParseTagsFromAzureResponse(json);
            }
        }

        private List<string> ParseTagsFromAzureResponse(string json)
        {
            var tags = new List<string>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tags", out var tagsElem))
                {
                    foreach (var tag in tagsElem.EnumerateArray())
                    {
                        if (tag.TryGetProperty("name", out var nameElem))
                            tags.Add(nameElem.GetString());
                    }
                }
                if (doc.RootElement.TryGetProperty("description", out var descElem))
                {
                    if (descElem.TryGetProperty("tags", out var descTagsElem))
                    {
                        foreach (var tag in descTagsElem.EnumerateArray())
                            tags.Add(tag.GetString());
                    }
                }
            }
            catch { }
            return tags.Distinct().ToList();
        }

        private List<object> RecognizeMenuItemsFromTags(List<string> tags)
        {
            var results = new List<object>();
            if (tags == null || tags.Count == 0) return results;
            var menuItems = _db.MenuItems.ToList();
            foreach (var item in menuItems)
            {
                foreach (var tag in tags)
                {
                    if (item.Name.Contains(tag, StringComparison.OrdinalIgnoreCase) ||
                        item.Description.Contains(tag, StringComparison.OrdinalIgnoreCase) ||
                        (item.PhotoURL != null && item.PhotoURL.Contains(tag, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add(new { name = item.Name });
                        break;
                    }
                }
            }
            return results;
        }

        #region Existing Helper Methods

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
                return "We offer delivery within a 10km radius. Delivery is free for all orders. Estimated delivery time is 30-45 minutes depending on your location. <a href='/Cart' target='_blank' style='color:#2196F3;font-weight:bold;'>View your cart</a> when you're ready to checkout and select your delivery options.";
            }

            // Payment methods
            if (lowerMessage.Contains("payment") || lowerMessage.Contains("pay") || lowerMessage.Contains("credit") || lowerMessage.Contains("card"))
            {
                // Add direct link to checkout
                return "We accept all major credit cards, online banking, and cash on delivery. You can securely save your payment method for faster checkout next time. <a href='/Cart' target='_blank' style='color:#2196F3;font-weight:bold;'>Check your cart</a> when you're ready to complete your order through .";
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

            // Hours/operating time
            if (lowerMessage.Contains("hour") || lowerMessage.Contains("open") || lowerMessage.Contains("close") || lowerMessage.Contains("time"))
            {
                return "We're open daily from 8:00 AM to 11:00 PM! Our kitchen stays busy all day to serve you fresh, delicious meals. Late night cravings? We've got you covered until 11 PM!";
            }

            // Location/address
            if (lowerMessage.Contains("location") || lowerMessage.Contains("address") || lowerMessage.Contains("where are you"))
            {
                return "We're located in the heart of the city, ready to serve you! For delivery, we cover a 10km radius. You can find our exact location and contact details on our website. Is there anything specific about our location you'd like to know?";
            }

            // Nutrition/health
            if (lowerMessage.Contains("calorie") || lowerMessage.Contains("nutrition") || lowerMessage.Contains("healthy") || lowerMessage.Contains("diet"))
            {
                return "We care about your health! Many of our menu items include nutritional information, and we offer healthy options like fresh salads and grilled items. Have specific dietary requirements? Let me know and I'll help you find the perfect meal!";
            }

            // Allergies/dietary restrictions
            if (lowerMessage.Contains("allerg") || lowerMessage.Contains("gluten") || lowerMessage.Contains("vegan") || lowerMessage.Contains("vegetarian"))
            {
                return "Your dietary needs are important to us! We have options for various dietary restrictions including vegetarian, vegan, and gluten-free choices. Please let us know about any allergies when ordering so we can ensure your meal is prepared safely.";
            }

            // Prices/cost
            if (lowerMessage.Contains("price") || lowerMessage.Contains("cost") || lowerMessage.Contains("cheap") || lowerMessage.Contains("expensive"))
            {
                return "We believe in providing great value for delicious food! Our prices are competitive and we often have special offers. You can see all prices on our menu, and we offer free delivery for orders over RM50. What would you like to know about our pricing?";
            }

            // Staff/employment
            if (lowerMessage.Contains("job") || lowerMessage.Contains("work") || lowerMessage.Contains("employ") || lowerMessage.Contains("hiring"))
            {
                return "Interested in joining the QuickBite team? We're always looking for passionate people who love food and customer service! Please check our careers page or visit us in person to learn about current opportunities.";
            }

            // Reviews/feedback
            if (lowerMessage.Contains("review") || lowerMessage.Contains("feedback") || lowerMessage.Contains("rating") || lowerMessage.Contains("opinion"))
            {
                return "Your feedback means everything to us! We love hearing from our customers about their experiences. You can leave reviews on our website, social media, or just tell me right here. What would you like to share?";
            }

            // Default response with more personality
            var defaultResponses = new[]
            {
                "I'm here to help make your QuickBite experience amazing! What can I assist you with today?",
                "How can I help you discover something delicious at QuickBite today?",
                "What brings you to QuickBite today? I'm here to help with orders, questions, or recommendations!",
                "Ready for some great food? Let me know how I can help you with your QuickBite experience!",
                "Welcome to QuickBite! Whether you're hungry now or just browsing, I'm here to help. What interests you?"
            };

            return defaultResponses[new Random().Next(defaultResponses.Length)];
        }

        #endregion

        public async Task ModifyMenuItem(string itemName, string field, string newValue)
        {
            try
            {
                var menuItem = _db.MenuItems.FirstOrDefault(m => m.Name.ToLower() == itemName.ToLower());
                if (menuItem == null)
                {
                    await Clients.Caller.SendAsync("MenuItemModified", $"Menu item '{itemName}' not found.");
                    return;
                }
                switch (field.ToLower())
                {
                    case "name":
                        menuItem.Name = newValue;
                        break;
                    case "description":
                        menuItem.Description = newValue;
                        break;
                    case "price":
                        if (decimal.TryParse(newValue, out var price))
                            menuItem.Price = price;
                        else
                        {
                            await Clients.Caller.SendAsync("MenuItemModified", "Invalid price value.");
                            return;
                        }
                        break;
                    case "photourl":
                        menuItem.PhotoURL = newValue;
                        break;
                    default:
                        await Clients.Caller.SendAsync("MenuItemModified", $"Field '{field}' is not supported for modification.");
                        return;
                }
                _db.SaveChanges();
                await Clients.Caller.SendAsync("MenuItemModified", $"Menu item '{itemName}' updated successfully.");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("MenuItemModified", $"Error: {ex.Message}");
            }
        }

        public async Task ModifyCategory(string categoryName, string newName)
        {
            try
            {
                var category = _db.Categories.FirstOrDefault(c => c.Name.ToLower() == categoryName.ToLower());
                if (category == null)
                {
                    await Clients.Caller.SendAsync("CategoryModified", $"Category '{categoryName}' not found.");
                    return;
                }
                category.Name = newName;
                _db.SaveChanges();
                await Clients.Caller.SendAsync("CategoryModified", $"Category '{categoryName}' renamed to '{newName}'.");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("CategoryModified", $"Error: {ex.Message}");
            }
        }

        public async Task TrackOrderLive(string userRole, string userIdentifier, string orderNumber)
        {
            // Find the order for the user
            if (!int.TryParse(orderNumber, out int orderId))
            {
                await Clients.Caller.SendAsync("LiveOrderStatus", new { message = $"Invalid order number." });
                return;
            }
            var order = _db.Orders.FirstOrDefault(o => o.OrderId == orderId && (userRole == "Admin" || o.MemberEmail == userIdentifier));
            if (order == null)
            {
                await Clients.Caller.SendAsync("LiveOrderStatus", new { message = $"Order #{orderNumber} not found." });
                return;
            }
            // Build timeline/status HTML
            var timeline = $@"<div><b>Order #{order.OrderId}</b><br>Status: <b>{order.Status}</b><br>Placed: {order.OrderDate:yyyy-MM-dd HH:mm}</div>";
            // Estimate time (simple logic: 1 min for Paid, 5 min for Preparing, 10 min for Out for Delivery)
            string estimated = order.Status switch {
                "Paid" => $"{order.OrderDate.AddMinutes(1):h:mm tt}",
                "Preparing" => $"{order.OrderDate.AddMinutes(5):h:mm tt}",
                "Out for Delivery" => $"{order.OrderDate.AddMinutes(10):h:mm tt}",
                "Delivered" => "Delivered!",
                _ => "Unknown"
            };
            var timelineHtml = $@"<div style='padding:10px;border-radius:8px;background:#f4f8fb;margin:10px 0;'>
                <b>Status Timeline</b><br>
                <ul style='list-style:none;padding:0;'>
                    <li><b>Order Placed:</b> {order.OrderDate:yyyy-MM-dd HH:mm}</li>
                    <li><b>Status:</b> {order.Status}</li>
                    <li><b>Estimated Time:</b> {estimated}</li>
                </ul>
            </div>";
            await Clients.Caller.SendAsync("LiveOrderStatus", new {
                timelineHtml,
                estimatedTime = estimated,
                suggestions = new[] { "Reorder previous order", "Show menu", "View cart" }
            });
        }

        public async Task ReorderPreviousOrder(string userRole, string userIdentifier)
        {
            // Find the most recent order for the user
            var order = _db.Orders
                .Where(o => userRole == "Admin" || o.MemberEmail == userIdentifier)
                .OrderByDescending(o => o.OrderDate)
                .FirstOrDefault();
            if (order == null)
            {
                await Clients.Caller.SendAsync("ReorderResult", "No previous order found to reorder.");
                return;
            }
            // Remove existing cart items for this user
            var cartItems = _db.CartItems.Where(ci => ci.MemberEmail == userIdentifier).ToList();
            _db.CartItems.RemoveRange(cartItems);
            // Add items from previous order
            var orderItems = _db.OrderItems.Where(oi => oi.OrderId == order.OrderId).ToList();
            foreach (var item in orderItems)
            {
                _db.CartItems.Add(new Models.CartItem
                {
                    MemberEmail = userIdentifier,
                    MenuItemId = item.MenuItemId,
                    Quantity = item.Quantity
                });
            }
            _db.SaveChanges();
            await Clients.Caller.SendAsync("ReorderResult", $"Previous order #{order.OrderId} has been added to your cart. <a href='/Cart'>View cart</a> to proceed to checkout.");
        }

        public async Task SendFeedback(string userRole, string userIdentifier, string rating)
        {
            // Save feedback to DB (if you have a Feedback table)
            // For demo, just send a thank you message and log
            await Clients.Caller.SendAsync("ReceiveResponse", $"Feedback received: {rating} star(s). Thank you!");
            // Optionally, show suggestions after feedback
            await Clients.Caller.SendAsync("ShowSuggestions", new[] { "Order food", "Show menu", "Track my order", "Contact support" });
        }
    }
}