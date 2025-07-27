using Microsoft.AspNetCore.Mvc;
using Demo.Models;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace Demo.Controllers;

public class CartController : Controller
{
    private readonly DB db;
    public CartController(DB db)
    {
        this.db = db;
    }

    // Session key for cart
    private const string CartSessionKey = "CartItems";

    // POST: /Cart/Add
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Add(int menuItemId, int quantity)
    {
        if (!User.IsInRole("Member"))
            return Unauthorized();
        if (quantity < 1) quantity = 1;
        var menuItem = db.MenuItems.Find(menuItemId);
        if (menuItem == null) return NotFound();

        // Get cart from session
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        var existing = cart.FirstOrDefault(x => x.MenuItemId == menuItemId);
        if (existing != null)
            existing.Quantity += quantity;
        else
            cart.Add(new CartItemVM { MenuItemId = menuItemId, Name = menuItem.Name, Price = menuItem.Price, Quantity = quantity });
        HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
        TempData["Info"] = $"Added {quantity} x {menuItem.Name} to cart.";
        return Redirect(Request.Headers["Referer"].ToString());
    }

    // GET: /Cart
    public IActionResult Index()
    {
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        return View(cart);
    }

    // GET: /Cart/Payment
    public IActionResult Payment()
    {
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        if (!cart.Any())
        {
            TempData["Info"] = "Cart is empty.";
            return RedirectToAction("Index");
        }
        var vm = new PaymentVM {
            Total = cart.Sum(x => x.Price * x.Quantity)
        };
        return View(vm);
    }

    // POST: /Cart/Payment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Payment(PaymentVM vm)
    {
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        if (!cart.Any())
        {
            TempData["Info"] = "Cart is empty.";
            return RedirectToAction("Index");
        }
        if (vm.PaymentMethod == "Cash")
        {
            ModelState.Remove(nameof(vm.CardNumber)); // Remove validation errors for CardNumber
            vm.CardNumber = null; // Also ensure the property is null to avoid any implicit issues
        }
        if (!ModelState.IsValid)
        {
            vm.Total = cart.Sum(x => x.Price * x.Quantity);
            return View(vm);
        }
        var member = db.Members.Find(User.Identity!.Name);
        if (member == null) return Unauthorized();
        var order = new Order
        {
            MemberEmail = member.Email,
            Status = "Paid",
            OrderItems = cart.Select(x => new OrderItem
            {
                MenuItemId = x.MenuItemId,
                Quantity = x.Quantity,
                UnitPrice = x.Price
            }).ToList()
        };
        db.Orders.Add(order);
        db.SaveChanges();
        HttpContext.Session.Remove(CartSessionKey);
        // Pass order id to receipt
        return RedirectToAction("Receipt", new { id = order.OrderId });
    }

    // GET: /Cart/Receipt/{id}
    public IActionResult Receipt(int id)
    {
        var order = db.Orders
            .Where(o => o.OrderId == id)
            .Select(o => new ReceiptVM
            {
                OrderId = o.OrderId,
                Date = o.OrderDate,
                Items = o.OrderItems.Select(oi => new CartItemVM
                {
                    MenuItemId = oi.MenuItemId,
                    Name = oi.MenuItem.Name,
                    Price = oi.UnitPrice,
                    Quantity = oi.Quantity
                }).ToList(),
                Total = o.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity)
            })
            .FirstOrDefault();
        if (order == null) return NotFound();
        return View(order);
    }

    // POST: /Cart/Remove
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int menuItemId)
    {
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();
        cart.RemoveAll(x => x.MenuItemId == menuItemId);
        HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
        return RedirectToAction("Index");
    }

    // GET: /Cart/History
    [HttpGet]
    public IActionResult History()
    {
        if (!User.IsInRole("Member")) return Unauthorized();
        var email = User.Identity!.Name;
        var orders = db.Orders
            .Where(o => o.MemberEmail == email)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new OrderHistoryVM
            {
                OrderId = o.OrderId,
                Date = o.OrderDate,
                Status = o.Status,
                Total = o.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity)
            })
            .ToList();
        return View(orders);
    }
}

// Cart item view model
public class CartItemVM
{
    public int MenuItemId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class PaymentVM : IValidatableObject
{
    public decimal Total { get; set; }
    [Required(ErrorMessage = "Please select a payment method.")]
    public string PaymentMethod { get; set; } // e.g. Cash, Card
    public string CardNumber { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PaymentMethod == "Card")
        {
            if (string.IsNullOrWhiteSpace(CardNumber))
                yield return new ValidationResult("Card number is required for card payments.", new[] { nameof(CardNumber) });
            else if (CardNumber.Length != 16 || !CardNumber.All(char.IsDigit))
                yield return new ValidationResult("Card number must be 16 digits.", new[] { nameof(CardNumber) });
        }
    }
}

public class ReceiptVM
{
    public int OrderId { get; set; }
    public DateTime Date { get; set; }
    public List<CartItemVM> Items { get; set; }
    public decimal Total { get; set; }
}

public class OrderHistoryVM
{
    public int OrderId { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; }
    public decimal Total { get; set; }
}
