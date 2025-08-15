using Microsoft.AspNetCore.Mvc;
using WebApplication2.Models; // Assuming your DBContext and Models are here
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations; // For [Required], [CreditCard], [RegularExpression], [Display]
using Microsoft.EntityFrameworkCore; // For .Include() and .FirstOrDefaultAsync()
using System.Threading.Tasks; // For async/await
using System; // For Math.Max, DateTime.Now
using System.Globalization;
using Microsoft.AspNetCore.Authorization; // For [Authorize] attribute
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace WebApplication2.Controllers;

public class CartController : Controller
{
    private readonly DB _db; // Renamed db to _db for consistency

    public CartController(DB db)
    {
        _db = db;
    }

    private const string CartSessionKey = "CartItems";

    // GET: /Cart/Index (Displays the cart page)
    public IActionResult Index()
    {
        // Retrieve cart items from session
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey);
        if (cart == null || !cart.Any())
        {
            // Try to load from cookie if session cart is empty
            cart = CartCookieHelper.LoadCartFromCookie(HttpContext);
            HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
        }
        return View(cart);
    }

    // POST: /Cart/Add (Modified to return JSON for AJAX calls from MenuItem/Index)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Add(int menuItemId, int quantity, string? SelectedPersonalizations)
    {
        // Basic authorization check
        if (!User.IsInRole("Member"))
        {
            return Json(new { success = false, message = "Unauthorized access." });
        }

        // Validate quantity
        if (quantity < 1) quantity = 1;

        // Find the menu item in the database
        var menuItem = _db.MenuItems.Find(menuItemId);
        if (menuItem == null)
        {
            return Json(new { success = false, message = "Menu item not found." });
        }

        // Retrieve or initialize the cart from session
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        // Find existing item with same MenuItemId and same SelectedPersonalizations
        var existing = cart.FirstOrDefault(x => x.MenuItemId == menuItemId && (x.SelectedPersonalizations ?? "") == (SelectedPersonalizations ?? ""));

        if (existing != null)
        {
            // Update quantity, capping at 100 for example
            existing.Quantity = Math.Min(100, existing.Quantity + quantity);
        }
        else
        {
            // Add new item to cart
            cart.Add(new CartItemVM
            {
                MenuItemId = menuItemId,
                Name = menuItem.Name,
                Price = menuItem.Price,
                Quantity = quantity,
                PhotoURL = menuItem.PhotoURL ?? "default.jpg",
                SelectedPersonalizations = SelectedPersonalizations
            });
        }

        // Save updated cart back to session
        HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
        CartCookieHelper.SaveCartToCookie(HttpContext, cart); // Save to cookie

        // Calculate current total (optional, but useful for client-side updates)
        decimal currentCartTotal = cart.Sum(x => x.Price * x.Quantity);

        // Return a JSON response for AJAX calls
        return Json(new { success = true, message = $"Added {quantity} x {menuItem.Name} to cart.", newTotal = currentCartTotal });
    }

    // NEW ACTION: POST to update item quantity via AJAX from Cart/Index
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateQuantity([FromBody] CartUpdateModel model)
    {
        if (!User.IsInRole("Member"))
            return Json(new { success = false, message = "Unauthorized." });

        if (model == null || model.MenuItemId <= 0 || model.Quantity < 1)
        {
            return Json(new { success = false, message = "Invalid data provided." });
        }

        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        var itemToUpdate = cart.FirstOrDefault(x => x.MenuItemId == model.MenuItemId);

        if (itemToUpdate == null)
        {
            return Json(new { success = false, message = "Item not found in cart." });
        }

        // Ensure quantity is within valid range (e.g., min 1, max 100)
        itemToUpdate.Quantity = Math.Max(1, model.Quantity);
        if (itemToUpdate.Quantity > 100) itemToUpdate.Quantity = 100;

        HttpContext.Session.SetObjectAsJson(CartSessionKey, cart); // Save updated cart to session
        CartCookieHelper.SaveCartToCookie(HttpContext, cart); // Save to cookie

        return Json(new { success = true, newTotal = cart.Sum(x => x.Price * x.Quantity) });
    }

    // NEW ACTION: POST to remove item via AJAX from Cart/Index
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveItem([FromBody] CartUpdateModel model)
    {
        if (!User.IsInRole("Member"))
            return Json(new { success = false, message = "Unauthorized." });

        if (model == null || model.MenuItemId <= 0)
        {
            return Json(new { success = false, message = "Invalid item ID." });
        }

        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        var initialCount = cart.Count;
        cart.RemoveAll(x => x.MenuItemId == model.MenuItemId); // Remove the item

        if (cart.Count < initialCount) // Check if an item was actually removed
        {
            HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
            CartCookieHelper.SaveCartToCookie(HttpContext, cart); // Save to cookie
            return Json(new { success = true, message = "Item removed.", newTotal = cart.Sum(x => x.Price * x.Quantity) });
        }
        else
        {
            return Json(new { success = false, message = "Item not found in cart." });
        }
    }

    // POST: /Cart/Clear (Clears the entire cart)
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(CartSessionKey);
        CartCookieHelper.ClearCartCookie(HttpContext); // Clear cookie
        TempData["Info"] = "Cart cleared.";
        return RedirectToAction("Index");
    }

    // GET: /Cart/Payment (Displays the payment page, total calculated from session)
    public IActionResult Payment()
    {
        if (!User.IsInRole("Member"))
            return Unauthorized();
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey);
        if (cart == null || !cart.Any())
        {
            cart = CartCookieHelper.LoadCartFromCookie(HttpContext);
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

    // POST: /Cart/Payment (Processes payment and creates the order in DB)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment(PaymentVM vm)
    {
        if (!User.IsInRole("Member"))
            return Unauthorized();
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
        foreach (var item in cart)
        {
            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.OrderId,
                MenuItemId = item.MenuItemId,
                Quantity = item.Quantity,
                UnitPrice = item.Price,
                SelectedPersonalizations = item.SelectedPersonalizations
            });
        }
        await _db.SaveChangesAsync();
        TempData["LastPaymentMethod"] = vm.PaymentMethod;
        TempData["LastPhoneNumber"] = vm.PhoneNumber;
        TempData["LastDeliveryInstructions"] = vm.DeliveryInstructions;
        TempData["LastDeliveryOption"] = vm.DeliveryOption;
        TempData["LastDeliveryAddress"] = vm.DeliveryAddress;
        if (vm.PaymentMethod == "Card")
        {
            TempData["LastCardNumber"] = vm.CardNumber;
        }
        HttpContext.Session.Remove(CartSessionKey);
        CartCookieHelper.ClearCartCookie(HttpContext); // Clear cookie after order placed
        TempData["Success"] = $"Order #{order.OrderId} placed successfully! Thank you for your purchase.";
        return RedirectToAction("Receipt", new { id = order.OrderId });
    }

    // GET: /Cart/Receipt (Retrieves order details from the database for the receipt)
    public async Task<IActionResult> Receipt(int id)
    {
        if (!User.IsInRole("Member"))
            return Unauthorized();

        // Retrieve the order and its items from the database
        var order = await _db.Orders
                            .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.MenuItem)
                            .FirstOrDefaultAsync(o => o.OrderId == id && o.MemberEmail == User.Identity.Name);

        if (order == null)
        {
            TempData["Error"] = "Order not found or you do not have access.";
            return RedirectToAction("MyOrders", "Account");
        }

        // Use payment method from order entity
        string paymentMethod = order.PaymentMethod ?? TempData["LastPaymentMethod"] as string ?? "-";
        string phoneNumber = TempData["LastPhoneNumber"] as string;
        string deliveryInstructions = TempData["LastDeliveryInstructions"] as string;
        string cardNumber = TempData["LastCardNumber"] as string;
        string deliveryOption = TempData["LastDeliveryOption"] as string;
        string deliveryAddress = order.DeliveryAddress ?? TempData["LastDeliveryAddress"] as string;

        // Map database entities to the ReceiptVM for display
        var receiptItems = order.OrderItems.Select(oi => new CartItemVM
        {
            MenuItemId = oi.MenuItemId,
            Name = oi.MenuItem.Name,
            Price = oi.UnitPrice,
            Quantity = oi.Quantity,
            SelectedPersonalizations = oi.SelectedPersonalizations // Ensure this is mapped if present in your OrderItem entity
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
            DeliveryAddress = deliveryAddress // Set delivery address
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult GetFilteredCartItems(string? search, decimal? minPrice, decimal? maxPrice)
    {
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        var fullTotal = cart.Sum(x => x.Price * x.Quantity);

        var query = cart.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (minPrice.HasValue)
        {
            query = query.Where(m => m.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(m => m.Price <= maxPrice.Value);
        }

        var items = query
            .OrderBy(m => m.Name)
            .Select(m => new
            {
                menuItemId = m.MenuItemId,
                name = m.Name,
                photoURL = m.PhotoURL,
                price = m.Price,
                quantity = m.Quantity,
                selectedPersonalizations = m.SelectedPersonalizations
            })
            .ToList();

        return Json(new { items, fullTotal });
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

    // POST: /Cart/History
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
}

// --- ViewModels (can be moved to a separate ViewModels folder) ---

// ViewModel for AJAX updates (quantity and remove operations)
public class CartUpdateModel
{
    public int MenuItemId { get; set; }
    public int Quantity { get; set; } // Only needed for UpdateQuantity, will be default for RemoveItem
}

// ViewModel for individual cart items (used in session, Cart/Index, Cart/Receipt)
public class CartItemVM
{
    public int MenuItemId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string PhotoURL { get; set; }  // Added for displaying item images
    public string? SelectedPersonalizations { get; set; } // Comma-separated personalization option names
}

// ViewModel for the payment page
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

    // --- Enhancement: Delivery Address for this order ---
    [Display(Name = "Delivery Address")]
    [Required(ErrorMessage = "Delivery address is required.")]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
    public string DeliveryAddress { get; set; }

    [Display(Name = "Phone Number")]
    [RegularExpression(@"^\d{10,12}$", ErrorMessage = "Please enter a valid phone number")]
    [Required(ErrorMessage = "Phone number is required for order updates")]
    public string PhoneNumber { get; set; }

    [Display(Name = "Delivery Instructions")]
    [StringLength(500, ErrorMessage = "Delivery instructions cannot exceed 500 characters")]
    public string? DeliveryInstructions { get; set; }

    [Display(Name = "Delivery Option")]
    [Required(ErrorMessage = "Please select a delivery option.")]
    public string DeliveryOption { get; set; } // "Delivery" or "Pickup"

    public decimal Total { get; set; } // Set by the controller based on session cart
    public List<CartItemVM> CartItems { get; set; } = new List<CartItemVM>(); // Add this property
}

// ViewModel for the receipt page
public class ReceiptVM
{
    public int OrderId { get; set; }
    public DateTime Date { get; set; }
    public List<CartItemVM> Items { get; set; } = new List<CartItemVM>(); // Initialize list
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } // Add payment method
    public string MemberEmail { get; set; }   // Add member email
    public string Status { get; set; }        // Add order status

    // New fields from payment information
    public string PhoneNumber { get; set; }
    public string DeliveryInstructions { get; set; }
    public string CardNumber { get; set; }  // Only last 4 digits will be displayed
    public string DeliveryOption { get; set; } // Add delivery option
    // --- Enhancement: Show delivery address used ---
    public string DeliveryAddress { get; set; }
}

public class OrderHistoryVM
{
    public List<OrderSummaryVM> Orders { get; set; } = new List<OrderSummaryVM>();
}

public class OrderSummaryVM // A nested ViewModel for each individual order in the history
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
    public List<OrderItemVM> Items { get; set; } = new List<OrderItemVM>(); // Details for each item in the order
    // --- Enhancement: Show delivery address used ---
    public string DeliveryAddress { get; set; }
    public string DeliveryOption { get; set; } // Add delivery option
}

public class OrderItemVM // A ViewModel for items within an order history record
{
    public string MenuItemName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; } // Price at the time of order
    public string PhotoURL { get; set; } // Optional: for displaying item photos in history
    public string? SelectedPersonalizations { get; set; } // Add this property for personalization display
}

public class TrackOrderVM
{
    [Required]
    public string OrderNumber { get; set; }
    public string? Address { get; set; }
    public List<OrderDetailsVM> Orders { get; set; } = new List<OrderDetailsVM>();
    public bool IsPostBack { get; set; }
}

public class OrderDetailsVM
{
    public string OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; }
    public string DeliveryOption { get; set; } // Add delivery option
}

// --- CART COOKIE HELPER ---
public static class CartCookieHelper
{
    private const string CartCookieKey = "MemberCart";
    public static void SaveCartToCookie(HttpContext context, List<CartItemVM> cart)
    {
        var options = new CookieOptions { Expires = DateTimeOffset.Now.AddDays(30), HttpOnly = true };
        var json = JsonSerializer.Serialize(cart);
        context.Response.Cookies.Append(CartCookieKey, json, options);
    }
    public static List<CartItemVM> LoadCartFromCookie(HttpContext context)
    {
        var json = context.Request.Cookies[CartCookieKey];
        if (string.IsNullOrEmpty(json)) return new List<CartItemVM>();
        try { return JsonSerializer.Deserialize<List<CartItemVM>>(json) ?? new List<CartItemVM>(); }
        catch { return new List<CartItemVM>(); }
    }
    public static void ClearCartCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(CartCookieKey);
    }
}