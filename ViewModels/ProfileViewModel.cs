using Vibee.Models;

namespace Vibee.ViewModels
{
    public class ProfileViewModel
    {
        public ApplicationUser User { get; set; }
        public bool IsOwner { get; set; }
        public List<Post> Posts { get; set; }
        public int FriendCount { get; set; }
        public int PhotoCount { get; set; }
        public string FriendshipStatus { get; set; }
        public List<ApplicationUser> Friends { get; set; } = new List<ApplicationUser>();
    }
}