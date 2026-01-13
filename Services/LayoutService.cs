using Microsoft.EntityFrameworkCore;
using Vibee.Data;

namespace Vibee.Services
{
    public class LayoutService
    {
        private readonly ApplicationDbContext _context;

        public LayoutService(ApplicationDbContext context)
        {
            _context = context;
        }

        public string GetAppVersion()
        {
            var setting = _context.SystemSettings.FirstOrDefault(s => s.Key == "AppVersion");
            return setting?.Value ?? "1.0.0";
        }
    }
}