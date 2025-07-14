using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
