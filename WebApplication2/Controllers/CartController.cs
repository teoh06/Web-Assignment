using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WebApplication2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WebApplication2.Services; // Add this for IEmailService
// ReceiptVM is in the WebApplication2.Models namespace

namespace WebApplication2.Controllers
{
    public class CartController : Controller
    {
        private readonly DB _db;
        private readonly IEmailService _emailService; // Add this line
        private const string CartSessionKey = "CartItems";
        private const string CartCookieKey = "MemberCart";

        public CartController(DB db, IEmailService emailService) // Update constructor
        {
            _db = db;
            _emailService = emailService; // Initialize email service
        }

        // -------------------
        // Add item to cart (AJAX)
        // -------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add([FromBody] CartItemInputModel model)
        {
            if (model == null)
            {
                // Fallback to form data if not JSON
                return AddFromForm(Request.Form["menuItemId"].ToString(), Request.Form["quantity"].ToString(), Request.Form["SelectedPersonalizations"].ToString());
            }
            
            if (!User.IsInRole("Member"))
                return Json(new { success = false, message = "Unauthorized access." });

            int menuItemId = model.menuItemId;
            int quantity = model.quantity;
            string? SelectedPersonalizations = model.SelectedPersonalizations;
            
            if (quantity < 1) quantity = 1;

            var menuItem = _db.MenuItems.Find(menuItemId);
            if (menuItem == null)
                return Json(new { success = false, message = "Menu item not found." });
                
            // Check stock availability
            if (menuItem.StockQuantity <= 0)
                return Json(new { success = false, message = "This item is out of stock." });
                
            // Check if requested quantity exceeds available stock
            if (quantity > menuItem.StockQuantity)
                return Json(new { success = false, message = $"Only {menuItem.StockQuantity} items available in stock." });

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            var existing = cart.FirstOrDefault(x => x.MenuItemId == menuItemId && (x.SelectedPersonalizations ?? "") == (SelectedPersonalizations ?? ""));

            if (existing != null)
                existing.Quantity = Math.Min(100, existing.Quantity + quantity);
            else
                cart.Add(new CartItemVM
                {
                    MenuItemId = menuItemId,
                    Name = menuItem.Name,
                    Price = menuItem.Price,
                    Quantity = quantity,
                    PhotoURL = menuItem.PhotoURL ?? "default.jpg",
                    SelectedPersonalizations = SelectedPersonalizations
                });

            HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
            SaveCartToCookie(cart);

            decimal currentCartTotal = cart.Sum(x => x.Price * x.Quantity);
            return Json(new { success = true, message = $"Added {quantity} x {menuItem.Name} to cart.", newTotal = currentCartTotal });
        }

        // -------------------
        // Display cart page
        // -------------------
        public IActionResult Index()
        {
            RemoveInactiveItemsFromCart();
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey);
            if (cart == null || !cart.Any())
            {
                cart = LoadCartFromCookie();
                HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
            }
            return View(cart);
        }

        // Helper method for form-based cart additions
        private IActionResult AddFromForm(string menuItemIdStr, string quantityStr, string? selectedPersonalizations)
        {
            if (!int.TryParse(menuItemIdStr, out int menuItemId) || !int.TryParse(quantityStr, out int quantity))
                return Json(new { success = false, message = "Invalid input data." });
                
            if (!User.IsInRole("Member"))
                return Json(new { success = false, message = "Unauthorized access." });

            if (quantity < 1) quantity = 1;

            var menuItem = _db.MenuItems.Find(menuItemId);
            if (menuItem == null)
                return Json(new { success = false, message = "Menu item not found." });
                
            // Check stock availability
            if (menuItem.StockQuantity <= 0)
                return Json(new { success = false, message = "This item is out of stock." });
                
            // Check if requested quantity exceeds available stock
            if (quantity > menuItem.StockQuantity)
                return Json(new { success = false, message = $"Only {menuItem.StockQuantity} items available in stock." });

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            var existing = cart.FirstOrDefault(x => x.MenuItemId == menuItemId && (x.SelectedPersonalizations ?? "") == (selectedPersonalizations ?? ""));

            if (existing != null)
                existing.Quantity = Math.Min(100, existing.Quantity + quantity);
            else
                cart.Add(new CartItemVM
                {
                    MenuItemId = menuItemId,
                    Name = menuItem.Name,
                    Price = menuItem.Price,
                    Quantity = quantity,
                    PhotoURL = menuItem.PhotoURL ?? "default.jpg",
                    SelectedPersonalizations = selectedPersonalizations
                });

            HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
            SaveCartToCookie(cart);

            decimal currentCartTotal = cart.Sum(x => x.Price * x.Quantity);
            return Json(new { success = true, message = $"Added {quantity} x {menuItem.Name} to cart.", newTotal = currentCartTotal });
        }
        
        // -------------------
        // Update quantity (AJAX)
        // -------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateQuantity([FromBody] CartUpdateModel model)
        {
            if (!User.IsInRole("Member"))
                return Json(new { success = false, message = "Unauthorized." });

            if (model == null || model.MenuItemId <= 0 || model.Quantity < 1)
                return Json(new { success = false, message = "Invalid data provided." });
                
            // Check stock availability
            var menuItem = _db.MenuItems.Find(model.MenuItemId);
            if (menuItem == null)
                return Json(new { success = false, message = "Menu item not found." });
                
            if (menuItem.StockQuantity <= 0)
                return Json(new { success = false, message = "This item is out of stock." });
                
            if (model.Quantity > menuItem.StockQuantity)
                return Json(new { success = false, message = $"Only {menuItem.StockQuantity} items available in stock." });

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            var itemToUpdate = cart.FirstOrDefault(x => x.MenuItemId == model.MenuItemId);

            if (itemToUpdate == null)
                return Json(new { success = false, message = "Item not found in cart." });

            itemToUpdate.Quantity = Math.Clamp(model.Quantity, 1, 100);
            HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
            SaveCartToCookie(cart);

            return Json(new { success = true, newTotal = cart.Sum(x => x.Price * x.Quantity) });
        }

        // -------------------
        // Remove item (AJAX)
        // -------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveItem([FromBody] CartUpdateModel model)
        {
            if (!User.IsInRole("Member"))
                return Json(new { success = false, message = "Unauthorized." });

            if (model == null || model.MenuItemId <= 0)
                return Json(new { success = false, message = "Invalid item ID." });

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            var removed = cart.RemoveAll(x => x.MenuItemId == model.MenuItemId);

            if (removed > 0)
            {
                HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
                SaveCartToCookie(cart);
                return Json(new { success = true, message = "Item removed.", newTotal = cart.Sum(x => x.Price * x.Quantity) });
            }
            else
                return Json(new { success = false, message = "Item not found in cart." });
        }

        // -------------------
        // Clear cart
        // -------------------
        public IActionResult Clear()
        {
            HttpContext.Session.Remove(CartSessionKey);
            ClearCartCookie();
            TempData["Info"] = "Cart cleared.";
            return RedirectToAction("Index");
        }

        // -------------------
        // Payment page
        // -------------------
        public IActionResult Payment()
        {
            if (!User.IsInRole("Member"))
                return Unauthorized();

            RemoveInactiveItemsFromCart();

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey);
            if (cart == null || !cart.Any())
            {
                cart = LoadCartFromCookie();
                HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
            }
            var total = cart.Sum(item => item.Price * item.Quantity);

            if (total <= 0)
            {
                TempData["Error"] = "Your cart is empty or total is zero. Please add items to proceed.";
                return RedirectToAction("Index");
            }

            var member = _db.Members.FirstOrDefault(m => m.Email == User.Identity.Name);
            var vm = new PaymentVM
            {
                Total = total,
                CartItems = cart,
                DeliveryAddress = member?.Address ?? ""
            };
            return View(vm);
        }

        // -------------------
        // Process payment
        // -------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Payment(PaymentVM vm)
        {
            if (!User.IsInRole("Member"))
                return Unauthorized();

            RemoveInactiveItemsFromCart();

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            vm.Total = cart.Sum(item => item.Price * item.Quantity);
            vm.CartItems = cart;

            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == User.Identity.Name);
            if (string.IsNullOrWhiteSpace(vm.DeliveryAddress))
            {
                vm.DeliveryAddress = member?.Address ?? "";
            }
            if (vm.PaymentMethod == "Cash")
            {
                ModelState.Remove(nameof(vm.CardNumber));
                vm.CardNumber = null;
            }
            else if (vm.PaymentMethod == "Card")
            {
                if (string.IsNullOrWhiteSpace(vm.CardNumber))
                {
                    ModelState.AddModelError(nameof(vm.CardNumber), "Card number is required for card payment.");
                }
            }
            if (!ModelState.IsValid || vm.Total <= 0 || !cart.Any())
            {
                if (vm.Total <= 0 || !cart.Any())
                {
                    ModelState.AddModelError("", "Your cart is empty or total is zero. Cannot proceed with payment.");
                }
                return View(vm);
            }

            var order = new Order
            {
                MemberEmail = User.Identity.Name,
                OrderDate = DateTime.Now,
                Status = "Paid",
                PaymentMethod = vm.PaymentMethod,
                DeliveryAddress = vm.DeliveryAddress,
                DeliveryOption = vm.DeliveryOption
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // Check stock availability before finalizing order
            bool stockIssue = false;
            string stockErrorMessage = "";
            
            foreach (var item in cart)
            {
                var menuItem = await _db.MenuItems.FindAsync(item.MenuItemId);
                if (menuItem == null)
                {
                    stockIssue = true;
                    stockErrorMessage = $"Menu item {item.Name} no longer exists.";
                    break;
                }
                
                if (menuItem.StockQuantity < item.Quantity)
                {
                    stockIssue = true;
                    stockErrorMessage = $"Not enough stock for {item.Name}. Only {menuItem.StockQuantity} available.";
                    break;
                }
            }
            
            if (stockIssue)
            {
                // Remove the order we just created
                _db.Orders.Remove(order);
                await _db.SaveChangesAsync();
                
                ModelState.AddModelError("", stockErrorMessage);
                return View(vm);
            }
            
            foreach (var item in cart)
            {
                // Add order item
                _db.OrderItems.Add(new OrderItem
                {
                    OrderId = order.OrderId,
                    MenuItemId = item.MenuItemId,
                    Quantity = item.Quantity,
                    UnitPrice = item.Price,
                    SelectedPersonalizations = item.SelectedPersonalizations
                });
                
                // Update stock quantity
                var menuItem = await _db.MenuItems.FindAsync(item.MenuItemId);
                menuItem.StockQuantity -= item.Quantity;
            }
            await _db.SaveChangesAsync();

            // Save info for receipt
            TempData["LastPaymentMethod"] = vm.PaymentMethod;
            TempData["LastPhoneNumber"] = vm.PhoneNumber;
            TempData["LastDeliveryInstructions"] = vm.DeliveryInstructions;
            TempData["LastDeliveryOption"] = vm.DeliveryOption;
            TempData["LastDeliveryAddress"] = vm.DeliveryAddress;
            if (vm.PaymentMethod == "Card")
            {
                TempData["LastCardNumber"] = vm.CardNumber;
            }

            // --- Send receipt email ---
            var receiptVm = new ReceiptVM
            {
                OrderId = order.OrderId,
                Date = order.OrderDate,
                Items = cart.Select(x => new CartItemVM
                {
                    MenuItemId = x.MenuItemId,
                    Name = x.Name,
                    Price = x.Price,
                    Quantity = x.Quantity,
                    SelectedPersonalizations = x.SelectedPersonalizations
                }).ToList(),
                Total = cart.Sum(x => x.Price * x.Quantity),
                PaymentMethod = vm.PaymentMethod,
                MemberEmail = member?.Email ?? User.Identity.Name,
                Status = order.Status,
                PhoneNumber = vm.PhoneNumber,
                DeliveryInstructions = vm.DeliveryInstructions,
                // Mask the card number for security - only show last 4 digits
                CardNumber = vm.PaymentMethod == "Card" && !string.IsNullOrEmpty(vm.CardNumber) 
                    ? "**** **** **** " + vm.CardNumber.Substring(Math.Max(0, vm.CardNumber.Length - 4)) 
                    : "",
                DeliveryOption = vm.DeliveryOption,
                DeliveryAddress = vm.DeliveryAddress
            };
            try
            {
                string html = await RenderViewToStringAsync("Receipt", receiptVm);
                
                // Use the injected email service instead of direct mail message
                string subject = $"Your Order Receipt - #{order.OrderId.ToString().PadLeft(6, '0')}";
                var recipient = member?.Email;
                if (string.IsNullOrWhiteSpace(recipient))
                    recipient = User.Identity.Name;
                
                // Ensure recipient is not null or empty
                if (string.IsNullOrWhiteSpace(recipient))
                    recipient = "bait2173.email@gmail.com";
                    
                // Log email sending attempt
                Console.WriteLine($"Attempting to send email to: {recipient}");
                await _emailService.SendEmailAsync(recipient, subject, html);
                TempData["Info"] = "A copy of your receipt has been sent to your email.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to send receipt email: {ex.Message}";
            }

            HttpContext.Session.Remove(CartSessionKey);
            ClearCartCookie();
            TempData["Success"] = $"Order #{order.OrderId} placed successfully! Thank you for your purchase.";
            return RedirectToAction("Receipt", new { id = order.OrderId });
        }

        // --- Helper to render Razor view to string for email ---
        private async Task<string> RenderViewToStringAsync(string viewName, object model)
        {
            var serviceProvider = HttpContext.RequestServices;
            var viewEngine = serviceProvider.GetService<ICompositeViewEngine>();
            var tempDataProvider = serviceProvider.GetService<ITempDataProvider>();
            var actionContext = new ActionContext(HttpContext, RouteData, ControllerContext.ActionDescriptor);
            using var sw = new StringWriter();
            
            // First try to find the view with the controller-specific path
            var viewResult = viewEngine.FindView(actionContext, $"Cart/{viewName}", false);
            
            // If not found, try to find the view without the controller prefix
            if (!viewResult.Success)
            {
                viewResult = viewEngine.FindView(actionContext, viewName, false);
            }
            
            // If still not found, throw an exception
            if (!viewResult.Success)
            {
                // Log more details about the view search
                Console.WriteLine($"Failed to find view '{viewName}'. Searched locations:");
                foreach (var location in viewResult.SearchedLocations)
                {
                    Console.WriteLine($"- {location}");
                }
                throw new InvalidOperationException($"View '{viewName}' not found. Make sure it exists in the Views/Cart directory.");
            }
            
            // Add ViewData for email rendering to indicate this is for email
            var viewDictionary = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };
            viewDictionary["IsEmailReceipt"] = true; // Flag to indicate this is for email
            var tempData = new TempDataDictionary(HttpContext, tempDataProvider);
            var viewContext = new ViewContext(actionContext, viewResult.View, viewDictionary, tempData, sw, new HtmlHelperOptions());
            await viewResult.View.RenderAsync(viewContext);
            return sw.ToString();
        }

        // -------------------
        // Receipt page
        // -------------------
        public async Task<IActionResult> Receipt(int id)
        {
            if (!User.IsInRole("Member"))
                return Unauthorized();

            var order = await _db.Orders
                                 .Include(o => o.OrderItems)
                                 .ThenInclude(oi => oi.MenuItem)
                                 .FirstOrDefaultAsync(o => o.OrderId == id && o.MemberEmail == User.Identity.Name);

            if (order == null)
            {
                TempData["Error"] = "Order not found or you do not have access.";
                return RedirectToAction("MyOrders", "Account");
            }

            // Get member information to ensure we have the correct phone number
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == User.Identity.Name);
            
            string paymentMethod = order.PaymentMethod ?? TempData["LastPaymentMethod"] as string ?? "-";
            string phoneNumber = TempData["LastPhoneNumber"] as string ?? member?.PhoneNumber;
            string deliveryInstructions = TempData["LastDeliveryInstructions"] as string;
            string cardNumber = TempData["LastCardNumber"] as string;
            string deliveryOption = TempData["LastDeliveryOption"] as string;
            string deliveryAddress = order.DeliveryAddress ?? TempData["LastDeliveryAddress"] as string;

            var receiptItems = order.OrderItems.Select(oi => new CartItemVM
            {
                MenuItemId = oi.MenuItemId,
                Name = oi.MenuItem.Name,
                Price = oi.UnitPrice,
                Quantity = oi.Quantity,
                SelectedPersonalizations = oi.SelectedPersonalizations
            }).ToList();

            var vm = new ReceiptVM
            {
                OrderId = order.OrderId,
                Date = order.OrderDate,
                Items = receiptItems,
                Total = receiptItems.Sum(item => item.Price * item.Quantity),
                PaymentMethod = paymentMethod,
                MemberEmail = order.MemberEmail,
                Status = order.Status,
                PhoneNumber = phoneNumber,
                DeliveryInstructions = deliveryInstructions,
                CardNumber = cardNumber,
                DeliveryOption = deliveryOption,
                DeliveryAddress = deliveryAddress
            };

            return View(vm);
        }

        // GET: /Cart/Track
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Track()
        {
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == User.Identity.Name);
            // --- Enhancement: Show recent orders for selection ---
            var recentOrders = await _db.Orders
                .Where(o => o.MemberEmail == member.Email)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .Select(o => new OrderDetailsVM
                {
                    OrderNumber = o.OrderId.ToString(),
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    DeliveryOption = o.DeliveryOption // Pass delivery option
                }).ToListAsync();

            var vm = new TrackOrderVM
            {
                Address = member?.Address,
                Orders = recentOrders,
                IsPostBack = false
            };
            return View(vm);
        }

        // POST: /Cart/Track
        [HttpPost]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Track(TrackOrderVM vm)
        {
            vm.IsPostBack = true;
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == User.Identity.Name);
            if (string.IsNullOrEmpty(vm.Address))
            {
                vm.Address = member?.Address;
            }
            if (string.IsNullOrWhiteSpace(vm.OrderNumber))
            {
                vm.Orders = await _db.Orders
                    .Where(o => o.MemberEmail == member.Email)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .Select(o => new OrderDetailsVM
                    {
                        OrderNumber = o.OrderId.ToString(),
                        OrderDate = o.OrderDate,
                        Status = o.Status,
                        DeliveryOption = o.DeliveryOption // Pass delivery option
                    }).ToListAsync();
                return PartialView("TrackResult", vm); // Return partial for AJAX
            }
            if (ModelState.IsValid)
            {
                if (int.TryParse(vm.OrderNumber, out int orderId))
                {
                    var orders = await _db.Orders
                        .Where(o => o.OrderId == orderId && o.MemberEmail == User.Identity.Name)
                        .Select(o => new OrderDetailsVM
                        {
                            OrderNumber = o.OrderId.ToString(),
                            OrderDate = o.OrderDate,
                            Status = o.Status,
                            DeliveryOption = o.DeliveryOption // Pass delivery option
                        })
                        .ToListAsync();
                    vm.Orders = orders;
                }
                else
                {
                    ModelState.AddModelError("OrderNumber", "Invalid order number format.");
                }
            }
            return PartialView("TrackResult", vm); // Return partial for AJAX
        }

        // -------------------
        // Order history
        // -------------------
        [HttpGet]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> History()
        {
            if (!User.IsInRole("Member"))
            {
                return Unauthorized();
            }
            var memberEmail = User.Identity.Name;
            var orders = await _db.Orders
                                  .Where(o => o.MemberEmail == memberEmail)
                                  .OrderByDescending(o => o.OrderDate)
                                  .Include(o => o.OrderItems)
                                      .ThenInclude(oi => oi.MenuItem)
                                  .ToListAsync();
            var orderHistoryVm = new OrderHistoryVM();
            foreach (var order in orders)
            {
                var orderSummary = new OrderSummaryVM
                {
                    OrderId = order.OrderId,
                    OrderDate = order.OrderDate,
                    Status = order.Status,
                    Items = order.OrderItems.Select(oi => new OrderItemVM
                    {
                        MenuItemName = oi.MenuItem?.Name,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        PhotoURL = oi.MenuItem?.PhotoURL,
                        SelectedPersonalizations = oi.SelectedPersonalizations
                    }).ToList(),
                    Total = order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity),
                    DeliveryAddress = order.DeliveryAddress,
                    DeliveryOption = order.DeliveryOption
                };
                orderHistoryVm.Orders.Add(orderSummary);
            }
            return View(orderHistoryVm);
        }

        // -------------------
        // AJAX filter cart
        // -------------------
        [HttpGet]
        public IActionResult GetFilteredCartItems(string? search, decimal? minPrice, decimal? maxPrice)
        {
            RemoveInactiveItemsFromCart();

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            var fullTotal = cart.Sum(x => x.Price * x.Quantity);

            var query = cart.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(m => m.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (minPrice.HasValue)
                query = query.Where(m => m.Price >= minPrice.Value);
            if (maxPrice.HasValue)
                query = query.Where(m => m.Price <= maxPrice.Value);

            var items = query.OrderBy(m => m.Name)
                             .Select(m => new
                             {
                                 menuItemId = m.MenuItemId,
                                 name = m.Name,
                                 photoURL = m.PhotoURL,
                                 price = m.Price,
                                 quantity = m.Quantity,
                                 selectedPersonalizations = m.SelectedPersonalizations
                             }).ToList();

            return Json(new { items, fullTotal });
        }

        // -------------------
        // Remove inactive items
        // -------------------
        private void RemoveInactiveItemsFromCart()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            var inactiveIds = _db.MenuItems.Where(m => !m.IsActive).Select(m => m.MenuItemId).ToHashSet();
            cart.RemoveAll(c => inactiveIds.Contains(c.MenuItemId));
            HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
            SaveCartToCookie(cart);
        }

        // --- Cookie helpers ---
        private void SaveCartToCookie(List<CartItemVM> cart)
        {
            var options = new CookieOptions { Expires = DateTimeOffset.Now.AddDays(30), HttpOnly = true };
            var json = JsonSerializer.Serialize(cart);
            HttpContext.Response.Cookies.Append(CartCookieKey, json, options);
        }
        private List<CartItemVM> LoadCartFromCookie()
        {
            var json = HttpContext.Request.Cookies[CartCookieKey];
            if (string.IsNullOrEmpty(json)) return new List<CartItemVM>();
            try { return JsonSerializer.Deserialize<List<CartItemVM>>(json) ?? new List<CartItemVM>(); }
            catch { return new List<CartItemVM>(); }
        }
        private void ClearCartCookie()
        {
            HttpContext.Response.Cookies.Delete(CartCookieKey);
        }

        // POST: /Cart/SendReceiptEmail
        [HttpPost]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReceiptEmail(int id)
        {
            // Log the request
            Console.WriteLine($"SendReceiptEmail requested for order ID: {id} by user: {User.Identity.Name}");
            
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.MemberEmail == User.Identity.Name);
                
            if (order == null)
            {
                TempData["Error"] = "Order not found or you do not have access.";
                return RedirectToAction("History");
            }
            
            // Get member information to ensure we have the correct email
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == User.Identity.Name);
            
            var receiptItems = order.OrderItems.Select(oi => new CartItemVM
            {
                MenuItemId = oi.MenuItemId,
                Name = oi.MenuItem.Name,
                Price = oi.UnitPrice,
                Quantity = oi.Quantity,
                SelectedPersonalizations = oi.SelectedPersonalizations
            }).ToList();
            
            var vm = new ReceiptVM
            {
                OrderId = order.OrderId,
                Date = order.OrderDate,
                Items = receiptItems,
                Total = receiptItems.Sum(item => item.Price * item.Quantity),
                PaymentMethod = order.PaymentMethod,
                MemberEmail = order.MemberEmail,
                Status = order.Status,
                PhoneNumber = member?.PhoneNumber,
                DeliveryInstructions = null,
                CardNumber = null, // For security, we don't include full card number in emails
                DeliveryOption = order.DeliveryOption,
                DeliveryAddress = order.DeliveryAddress
            };
            
            try
            {
                string html = await RenderViewToStringAsync("Receipt", vm);
                string subject = $"Your Order Receipt - #{order.OrderId.ToString().PadLeft(6, '0')}";
                
                // Use member email as primary recipient
                var recipient = member?.Email;
                
                // Fallback to order email if member email is not available
                if (string.IsNullOrWhiteSpace(recipient))
                    recipient = order.MemberEmail;
                    
                // Final fallback to user identity
                if (string.IsNullOrWhiteSpace(recipient))
                    recipient = User.Identity.Name;
                
                // Log the email recipient for debugging
                Console.WriteLine($"Sending receipt email to: {recipient}");
                
                await _emailService.SendEmailAsync(recipient, subject, html);
                TempData["Success"] = "A copy of your receipt has been sent to your email.";
                Console.WriteLine("Email sent successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending receipt email: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                TempData["Error"] = $"Failed to send receipt email: {ex.Message}";
            }
            
            return RedirectToAction("History");
        }

        // -------------------
        // Refund request page
        // -------------------
        [Authorize(Roles = "Member")]
        [HttpGet]
        public IActionResult Refund()
        {
            return View();
        }

        // POST: /Cart/GetDeliveryOption
        [HttpPost]
        public async Task<IActionResult> GetDeliveryOption(string OrderNumber, string Address)
        {
            if (string.IsNullOrWhiteSpace(OrderNumber))
            {
                return Json(new { success = false, message = "Order number is required." });
            }
            
            if (int.TryParse(OrderNumber, out int orderId))
            {
                var order = await _db.Orders
                    .Where(o => o.OrderId == orderId && o.MemberEmail == User.Identity.Name)
                    .FirstOrDefaultAsync();
                    
                if (order != null)
                {
                    return Json(new { success = true, deliveryOption = order.DeliveryOption });
                }
            }
            
            // Default to delivery if order not found
            return Json(new { success = false, deliveryOption = "Delivery" });
        }
        
        // This method was removed to fix the duplicate CancelOrder method
        
        // GET: /Cart/GetPurchaseSummaryData
        [Authorize(Roles = "Member")]
        [HttpGet]
        public async Task<IActionResult> GetPurchaseSummaryData()
        {
            try
            {
                // Get all orders for the current member
                var memberEmail = User.Identity.Name;
                var orders = await _db.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                    .ThenInclude(mi => mi.Category)
                    .Where(o => o.MemberEmail == memberEmail)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();
                
                // Monthly spending data (last 6 months)
                var today = DateTime.Today;
                var sixMonthsAgo = today.AddMonths(-6);
                
                var monthlyLabels = new List<string>();
                var monthlyData = new List<decimal>();
                
                for (int i = 0; i < 6; i++)
                {
                    var month = today.AddMonths(-i);
                    var monthName = month.ToString("MMM yyyy");
                    monthlyLabels.Insert(0, monthName);
                    
                    var monthlyTotal = orders
                        .Where(o => o.OrderDate.Year == month.Year && o.OrderDate.Month == month.Month)
                        .SelectMany(o => o.OrderItems)
                        .Sum(oi => oi.UnitPrice * oi.Quantity);
                    
                    monthlyData.Insert(0, monthlyTotal);
                }
                
                // Order categories data
                var categoryData = new List<int>();
                var categoryLabels = new List<string>();
                
                var categories = orders
                    .SelectMany(o => o.OrderItems)
                    .Select(oi => oi.MenuItem.Category)
                    .GroupBy(c => c.Name)
                    .Select(g => new { CategoryName = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToList();
                
                foreach (var category in categories)
                {
                    categoryLabels.Add(category.CategoryName);
                    categoryData.Add(category.Count);
                }
                
                return Json(new { 
                    monthlyLabels, 
                    monthlyData, 
                    categoryLabels, 
                    categoryData 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting purchase summary data: {ex.Message}");
                return Json(new { error = "Failed to load purchase summary data" });
            }
        }
        
        // GET: /Cart/GetOrderStatus
        [HttpGet]
        public async Task<IActionResult> GetOrderStatus(int orderId)
        {
            try
            {
                if (orderId <= 0)
                {
                    return Json(new { success = false, message = "Invalid order ID" });
                }
                
                var order = await _db.Orders
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);
                    
                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }
                
                // Check if the current user is authorized to view this order
                var currentUserEmail = User.Identity?.Name;
                var isAdmin = User.IsInRole("Admin");
                
                if (!isAdmin && order.MemberEmail != currentUserEmail)
                {
                    return Json(new { success = false, message = "You are not authorized to view this order" });
                }
                
                return Json(new
                {
                    success = true,
                    orderId = order.OrderId,
                    status = order.Status ?? "Unknown",
                    orderDate = order.OrderDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    deliveryOption = order.DeliveryOption ?? "Standard"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting order status: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while retrieving order status" });
            }
        }
        
        // POST: /Cart/Refund
        [Authorize(Roles = "Member")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Refund(OrderRefundVM vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == vm.OrderId && o.MemberEmail == User.Identity.Name);
            if (order == null)
            {
                ModelState.AddModelError("OrderId", "Order not found or you do not have access.");
                return View(vm);
            }
            if (order.Status == "Refunded" || order.Status == "Cancelled")
            {
                ModelState.AddModelError("OrderId", "Order is already refunded or cancelled.");
                return View(vm);
            }
            order.Status = "Refunded";
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Refund request for Order #{order.OrderId} submitted. Reason: {vm.Reason}";
            // Optionally, notify admin or save refund request to DB
            return RedirectToAction("History");
        }
        
        // POST: /Cart/CancelOrder
        [Authorize(Roles = "Member")]
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId, string reason)
        {
            try
            {
                // Find the order and verify ownership
                var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.MemberEmail == User.Identity.Name);
                
                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found or you do not have access." });
                }
                
                // Verify order can be cancelled (only Pending or Preparing orders)
                if (order.Status != "Pending" && order.Status != "Preparing")
                {
                    return Json(new { success = false, message = $"Cannot cancel order with status '{order.Status}'." });
                }
                
                // Update order status
                order.Status = "Cancelled";
                await _db.SaveChangesAsync();
                
                // Get member information for SMS notification
                var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == User.Identity.Name);
                string phoneNumber = member?.PhoneNumber;
                
                // Log cancellation with reason
                Console.WriteLine($"Order #{orderId} cancelled by {User.Identity.Name}. Reason: {reason}");
                
                // TODO: Implement actual SMS notification here
                // For now, we'll just log that we would send an SMS
                if (!string.IsNullOrEmpty(phoneNumber))
                {
                    Console.WriteLine($"SMS notification would be sent to {phoneNumber} for order cancellation #{orderId}");
                    // In a real implementation, you would call an SMS service here
                }
                
                return Json(new { success = true, message = "Order cancelled successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cancelling order: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while cancelling the order." });
            }
        }

        // --- WISH LIST ACTIONS ---
        [Authorize(Roles = "Member")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddToWishList(int menuItemId)
        {
            var email = User.Identity.Name;
            if (!_db.WishListItems.Any(w => w.MenuItemId == menuItemId && w.MemberEmail == email))
            {
                _db.WishListItems.Add(new WishListItem { MenuItemId = menuItemId, MemberEmail = email });
                _db.SaveChanges();
            }
            return Json(new { success = true });
        }

        [Authorize(Roles = "Member")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveFromWishList(int menuItemId)
        {
            var email = User.Identity.Name;
            var item = _db.WishListItems.FirstOrDefault(w => w.MenuItemId == menuItemId && w.MemberEmail == email);
            if (item != null)
            {
                _db.WishListItems.Remove(item);
                _db.SaveChanges();
            }
            return Json(new { success = true });
        }

        [Authorize(Roles = "Member")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TransferWishListToCart(int menuItemId, int quantity = 1)
        {
            var email = User.Identity.Name;
            var wish = _db.WishListItems.FirstOrDefault(w => w.MenuItemId == menuItemId && w.MemberEmail == email);
            var menuItem = _db.MenuItems.Find(menuItemId);
            if (wish == null || menuItem == null)
                return Json(new { success = false, message = "Item not found in wish list or menu." });
            if (menuItem.StockQuantity < quantity)
                return Json(new { success = false, message = $"Only {menuItem.StockQuantity} in stock." });
            // Add to cart (reuse Add logic)
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            var existing = cart.FirstOrDefault(x => x.MenuItemId == menuItemId);
            if (existing != null)
                existing.Quantity = Math.Min(100, existing.Quantity + quantity);
            else
                cart.Add(new CartItemVM
                {
                    MenuItemId = menuItemId,
                    Name = menuItem.Name,
                    Price = menuItem.Price,
                    Quantity = quantity,
                    PhotoURL = menuItem.PhotoURL ?? "default.jpg"
                });
            HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
            SaveCartToCookie(cart);
            // Remove from wish list after transfer
            _db.WishListItems.Remove(wish);
            _db.SaveChanges();
            return Json(new { success = true, message = $"Added {quantity} x {menuItem.Name} to cart and removed from wish list." });
        }
    }

    // =======================
    // ViewModels
    // =======================

    public class CartUpdateModel
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class CartItemVM
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string PhotoURL { get; set; }
        public string? SelectedPersonalizations { get; set; }
    }

    public class PaymentVM
    {
        [Display(Name = "Payment Method")]
        [Required(ErrorMessage = "Please select a payment method.")]
        public string PaymentMethod { get; set; }

        [Display(Name = "Card Number")]
        [RegularExpression(@"^\d{16}$", ErrorMessage = "Card number must be 16 digits.")]
        public string? CardNumber { get; set; }

        [Display(Name = "Card Holder Name")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string? CardHolderName { get; set; }

        [Display(Name = "Expiry Date")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/([0-9]{2})$", ErrorMessage = "Expiry date must be in MM/YY format")]
        public string? ExpiryDate { get; set; }

        [Display(Name = "CVV")]
        [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits")]
        public string? CVV { get; set; }

        [Display(Name = "Billing Address")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string? BillingAddress { get; set; }

        [Display(Name = "Phone Number")]
        [RegularExpression(@"^\d{10,12}$", ErrorMessage = "Please enter a valid phone number")]
        [Required(ErrorMessage = "Phone number is required")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Delivery Instructions")]
        [StringLength(500, ErrorMessage = "Delivery instructions cannot exceed 500 characters")]
        public string? DeliveryInstructions { get; set; }

        [Display(Name = "Delivery Option")]
        [Required(ErrorMessage = "Please select a delivery option.")]
        public string DeliveryOption { get; set; }

        [Display(Name = "Delivery Address")]
        [Required(ErrorMessage = "Delivery address is required.")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string DeliveryAddress { get; set; }

        public decimal Total { get; set; }
        public List<CartItemVM> CartItems { get; set; } = new List<CartItemVM>();
    }

    // ReceiptVM is now defined in Models/ViewModels.cs

    public class OrderHistoryVM
    {
        public List<OrderSummaryVM> Orders { get; set; } = new List<OrderSummaryVM>();
    }

    public class OrderSummaryVM
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }
        public List<OrderItemVM> Items { get; set; } = new List<OrderItemVM>();
        public string DeliveryAddress { get; set; }
        public string DeliveryOption { get; set; }
    }

    public class OrderItemVM
    {
        public string MenuItemName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string PhotoURL { get; set; }
        public string? SelectedPersonalizations { get; set; }
    }

    public class OrderRefundVM
    {
        [Required]
        [Display(Name = "Order Number")]
        public int OrderId { get; set; }

        [Required]
        [StringLength(500)]
        [Display(Name = "Reason for Refund")]
        public string Reason { get; set; }
    }
}
