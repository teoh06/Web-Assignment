using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using X.PagedList.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace WebApplication2.Controllers
{
    public class HomeController : Controller
    {

        public IActionResult Index()
        {
            return View();
        }

        // GET: Home/Both
        [Authorize]
        public IActionResult Both()
        {
            return View();
        }
        [Authorize(Roles = "Guest")]
        public IActionResult Guest()
        {
            return View();
        }

        // GET: Home/Member
        [Authorize(Roles = "Member")]
        public IActionResult Member()
        {
            return View();
        }

        // GET: Home/Admin
        [Authorize(Roles = "Admin")]
        public IActionResult Admin()
        {
            return View();
        }
    }
}
