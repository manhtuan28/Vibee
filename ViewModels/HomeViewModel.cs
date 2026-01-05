using Vibee.Models;

namespace Vibee.ViewModels
{
    public class HomeViewModel
    {
        public ApplicationUser? CurrentUser { get; set; }
        public List<Post> Posts { get; set; } = new List<Post>();
    }
}