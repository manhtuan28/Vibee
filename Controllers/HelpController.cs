using Microsoft.AspNetCore.Mvc;

namespace Vibee.Controllers
{
    public class HelpController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
