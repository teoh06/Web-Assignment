using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class CartController : Controller
    {
        private readonly DB _db;
        private const string CartSessionKey = "CartItems";

        public CartController(DB db)
        {
            _db = db;
        }

        // -------------------
        // Add item to cart (AJAX)
        // -------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(int menuItemId, int quantity, string? SelectedPersonalizations)
        {
            if (!User.IsInRole("Member"))
                return Json(new { success = false, message = "Unauthorized access." });

            if (quantity < 1) quantity = 1;

            var menuItem = _db.MenuItems.Find(menuItemId);
            if (menuItem == null)
                return Json(new { success = false, message = "Menu item not found." });

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

            decimal currentCartTotal = cart.Sum(x => x.Price * x.Quantity);
            return Json(new { success = true, message = $"Added {quantity} x {menuItem.Name} to cart.", newTotal = currentCartTotal });
        }

        // -------------------
        // Display cart page
        // -------------------
        public IActionResult Index()
        {
            RemoveInactiveItemsFromCart();
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            return View(cart);
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

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            var itemToUpdate = cart.FirstOrDefault(x => x.MenuItemId == model.MenuItemId);

            if (itemToUpdate == null)
                return Json(new { success = false, message = "Item not found in cart." });

            itemToUpdate.Quantity = Math.Clamp(model.Quantity, 1, 100);
            HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);

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

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
            var total = cart.Sum(item => item.Price * item.Quantity);

            if (total <= 0)
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Index");
            }

            var vm = new PaymentVM { Total = total, CartItems = cart };
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

            if (vm.PaymentMethod == "Cash")
                ModelState.Remove(nameof(vm.CardNumber));
            else if (vm.PaymentMethod == "Card" && string.IsNullOrWhiteSpace(vm.CardNumber))
                ModelState.AddModelError(nameof(vm.CardNumber), "Card number is required for card payment.");

            if (!ModelState.IsValid || !cart.Any())
            {
                ModelState.AddModelError("", "Your cart is empty or invalid data.");
                return View(vm);
            }

            var order = new Order
            {
                MemberEmail = User.Identity.Name,
                OrderDate = DateTime.Now,
                Status = "Paid",
                PaymentMethod = vm.PaymentMethod
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

            // Save info for receipt
            TempData["LastPaymentMethod"] = vm.PaymentMethod;
            TempData["LastPhoneNumber"] = vm.PhoneNumber;
            TempData["LastDeliveryInstructions"] = vm.DeliveryInstructions;
            TempData["LastDeliveryOption"] = vm.DeliveryOption;
            if (vm.PaymentMethod == "Card")
                TempData["LastCardNumber"] = vm.CardNumber;

            HttpContext.Session.Remove(CartSessionKey);
            TempData["Success"] = $"Order #{order.OrderId} placed successfully!";
            return RedirectToAction("Receipt", new { id = order.OrderId });
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
                TempData["Error"] = "Order not found.";
                return RedirectToAction("Index");
            }

            var receiptItems = order.OrderItems.Select(oi => new CartItemVM
            {
                MenuItemId = oi.MenuItemId,
                Name = oi.MenuItem?.Name ?? "-",
                Price = oi.UnitPrice,
                Quantity = oi.Quantity,
                SelectedPersonalizations = oi.SelectedPersonalizations
            }).ToList();

            var vm = new ReceiptVM
            {
                OrderId = order.OrderId,
                Date = order.OrderDate,
                Items = receiptItems,
                Total = receiptItems.Sum(i => i.Price * i.Quantity),
                PaymentMethod = order.PaymentMethod ?? TempData["LastPaymentMethod"] as string ?? "-",
                MemberEmail = order.MemberEmail,
                Status = order.Status,
                PhoneNumber = TempData["LastPhoneNumber"] as string,
                DeliveryInstructions = TempData["LastDeliveryInstructions"] as string,
                CardNumber = TempData["LastCardNumber"] as string,
                DeliveryOption = TempData["LastDeliveryOption"] as string
            };

            return View(vm);
        }

        // -------------------
        // Order history
        // -------------------
        public async Task<IActionResult> History()
        {
            if (!User.IsInRole("Member"))
                return Unauthorized();

            var memberEmail = User.Identity.Name;

            var orders = await _db.Orders
                                  .Where(o => o.MemberEmail == memberEmail)
                                  .OrderByDescending(o => o.OrderDate)
                                  .Include(o => o.OrderItems)
                                  .ThenInclude(oi => oi.MenuItem)
                                  .ToListAsync();

            var historyVm = new OrderHistoryVM();
            foreach (var order in orders)
            {
                historyVm.Orders.Add(new OrderSummaryVM
                {
                    OrderId = order.OrderId,
                    OrderDate = order.OrderDate,
                    Status = order.Status,
                    Total = order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity),
                    Items = order.OrderItems.Select(oi => new OrderItemVM
                    {
                        MenuItemName = oi.MenuItem?.Name ?? "-",
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        PhotoURL = oi.MenuItem?.PhotoURL ?? "default.jpg",
                        SelectedPersonalizations = oi.SelectedPersonalizations
                    }).ToList()
                });
            }

            return View(historyVm);
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

        public decimal Total { get; set; }
        public List<CartItemVM> CartItems { get; set; } = new List<CartItemVM>();
    }

    public class ReceiptVM
    {
        public int OrderId { get; set; }
        public DateTime Date { get; set; }
        public List<CartItemVM> Items { get; set; } = new List<CartItemVM>();
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; }
        public string MemberEmail { get; set; }
        public string Status { get; set; }
        public string PhoneNumber { get; set; }
        public string DeliveryInstructions { get; set; }
        public string CardNumber { get; set; }
        public string DeliveryOption { get; set; }
    }

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
    }

    public class OrderItemVM
    {
        public string MenuItemName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string PhotoURL { get; set; }
        public string? SelectedPersonalizations { get; set; }
    }
}
