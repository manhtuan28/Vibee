#nullable disable

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Vibee.Areas.Identity.Pages.Account
{
    public class AccessDeniedModel : PageModel
    {
        private readonly ILogger<AccessDeniedModel> _logger;

        public AccessDeniedModel(ILogger<AccessDeniedModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            if (User.Identity.IsAuthenticated)
            {
                _logger.LogWarning($"Người dùng '{User.Identity.Name}' đã bị từ chối truy cập vào tài nguyên này (403 Forbidden).");
            }
        }
    }
}