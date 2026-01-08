using Vibee.Models;

namespace Vibee.ViewModels
{
    public class FriendsPageViewModel
    {
        public ApplicationUser CurrentUser { get; set; }

        public List<ApplicationUser> FriendRequests { get; set; } = new List<ApplicationUser>();

        public List<ApplicationUser> Friends { get; set; } = new List<ApplicationUser>();

        public List<ApplicationUser> Suggestions { get; set; } = new List<ApplicationUser>();
        public List<string> SentRequestIds { get; set; } = new List<string>();

        public List<ApplicationUser> SentRequests { get; set; } = new List<ApplicationUser>();

    }
}