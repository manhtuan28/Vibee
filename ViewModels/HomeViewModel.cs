using Vibee.Models;

namespace Vibee.ViewModels
{
    public class HomeViewModel
    {
        public ApplicationUser CurrentUser { get; set; }
        public List<Post> Posts { get; set; } = new List<Post>();

        public List<ApplicationUser> SuggestFriends { get; set; } = new List<ApplicationUser>();
        public List<ApplicationUser> OnlineFriends { get; set; } = new List<ApplicationUser>();
    }
}