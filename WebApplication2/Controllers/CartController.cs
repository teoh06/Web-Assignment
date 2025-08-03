using Microsoft.AspNetCore.Mvc;
using WebApplication2.Models; // Assuming your DBContext and Models are here
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations; // For [Required], [CreditCard], [RegularExpression], [Display]
using Microsoft.EntityFrameworkCore; // For .Include() and .FirstOrDefaultAsync()
using System.Threading.Tasks; // For async/await
using System; // For Math.Max, DateTime.Now

namespace WebApplication2.Controllers;

public class CartController : Controller
{
    private readonly DB _db; // Renamed db to _db for consistency

    public CartController(DB db)
    {
        _db = db;
    }

    private const string CartSessionKey = "CartItems";

    // POST: /Cart/Add (Modified to return JSON for AJAX calls from MenuItem/Index)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Add(int menuItemId, int quantity)
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
        var existing = cart.FirstOrDefault(x => x.MenuItemId == menuItemId);

        if (existing != null)
        {
            // Update quantity, capping at 100 for example
            existing.Quantity = Math.Min(100, existing.Quantity + quantity);
        }
        else
        {
            // Add new item to cart
            cart.Add(new CartItemVM { MenuItemId = menuItemId, Name = menuItem.Name, Price = menuItem.Price, Quantity = quantity });
        }

        // Save updated cart back to session
        HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);

        // Calculate current total (optional, but useful for client-side updates)
        decimal currentCartTotal = cart.Sum(x => x.Price * x.Quantity);

        // Return a JSON response for AJAX calls
        return Json(new { success = true, message = $"Added {quantity} x {menuItem.Name} to cart.", newTotal = currentCartTotal });
    }

    // GET: /Cart/Index (Displays the cart page)
    public IActionResult Index()
    {
        // Retrieve cart items from session
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        return View(cart);
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
        TempData["Info"] = "Cart cleared.";
        return RedirectToAction("Index");
    }

    // GET: /Cart/Payment (Displays the payment page, total calculated from session)
    public IActionResult Payment()
    {
        if (!User.IsInRole("Member"))
            return Unauthorized();

        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        var total = cart.Sum(item => item.Price * item.Quantity);

        if (total <= 0)
        {
            TempData["Error"] = "Your cart is empty or total is zero. Please add items to proceed.";
            return RedirectToAction("Index"); // Redirect if cart is empty
        }

        var vm = new PaymentVM { Total = total };
        return View(vm);
    }

    public async Task<IActionResult> History() // Or MyOrders if that's your action name
    {
        if (!User.IsInRole("Member"))
        {
            return Unauthorized(); // Or RedirectToAction("Login", "Account");
        }

        var memberEmail = User.Identity.Name; // Get the logged-in member's email

        // Fetch orders for the current member from the database
        // Include OrderItems and their related MenuItems for display
        var orders = await _db.Orders
                              .Where(o => o.MemberEmail == memberEmail)
                              .OrderByDescending(o => o.OrderDate)
                              .Include(o => o.OrderItems)
                                  .ThenInclude(oi => oi.MenuItem) // Load MenuItem details for each order item
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
                    MenuItemName = oi.MenuItem?.Name, // Use null-conditional operator for safety
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    PhotoURL = oi.MenuItem?.PhotoURL // Optional: Photo for history
                }).ToList(),
                Total = order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity) // Calculate total
            };
            orderHistoryVm.Orders.Add(orderSummary);
        }

        return View(orderHistoryVm);
    }

    // POST: /Cart/Payment (Processes payment and creates the order in DB)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment(PaymentVM vm)
    {
        if (!User.IsInRole("Member"))
            return Unauthorized();

        // Security: Re-calculate total from session on the server to prevent client-side tampering
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        vm.Total = cart.Sum(item => item.Price * item.Quantity);

        // Adjust validation for CardNumber if PaymentMethod is Cash
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

        // Create a new Order record in the database
        var order = new Order
        {
            MemberEmail = User.Identity.Name,
            OrderDate = DateTime.Now,
            Status = "Paid"
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Add OrderItem records for each item in the cart
        foreach (var item in cart)
        {
            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.OrderId,
                MenuItemId = item.MenuItemId,
                Quantity = item.Quantity,
                UnitPrice = item.Price
            });
        }
        await _db.SaveChangesAsync();

        // Store payment information in TempData for the receipt
        TempData["LastPaymentMethod"] = vm.PaymentMethod;
        TempData["LastPhoneNumber"] = vm.PhoneNumber;
        TempData["LastDeliveryInstructions"] = vm.DeliveryInstructions;
        if (vm.PaymentMethod == "Card")
        {
            TempData["LastCardNumber"] = vm.CardNumber;
        }

        // Clear the cart
        HttpContext.Session.Remove(CartSessionKey);
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

        // Try to get payment method and other details from TempData
        string paymentMethod = TempData["LastPaymentMethod"] as string ?? "-";
        string phoneNumber = TempData["LastPhoneNumber"] as string;
        string deliveryInstructions = TempData["LastDeliveryInstructions"] as string;
        string cardNumber = TempData["LastCardNumber"] as string;

        // Map database entities to the ReceiptVM for display
        var receiptItems = order.OrderItems.Select(oi => new CartItemVM
        {
            MenuItemId = oi.MenuItemId,
            Name = oi.MenuItem.Name,
            Price = oi.UnitPrice,
            Quantity = oi.Quantity
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
            CardNumber = cardNumber
        };

        return View(vm);
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

    [Display(Name = "Phone Number")]
    [RegularExpression(@"^\d{10,12}$", ErrorMessage = "Please enter a valid phone number")]
    [Required(ErrorMessage = "Phone number is required for order updates")]
    public string PhoneNumber { get; set; }

    [Display(Name = "Delivery Instructions")]
    [StringLength(500, ErrorMessage = "Delivery instructions cannot exceed 500 characters")]
    public string? DeliveryInstructions { get; set; }

    public decimal Total { get; set; } // Set by the controller based on session cart
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
}

public class OrderItemVM // A ViewModel for items within an order history record
{
    public string MenuItemName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; } // Price at the time of order
    public string PhotoURL { get; set; } // Optional: for displaying item photos in history
}