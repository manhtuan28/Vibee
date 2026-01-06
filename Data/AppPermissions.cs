namespace Vibee.Data
{
    public static class AppPermissions
    {
        public static class Pages
        {
            public const string Dashboard = "Pages.Dashboard";
            public const string Users = "Pages.Users";
            public const string Roles = "Pages.Roles";
            public const string Wallets = "Pages.Wallets";
            public const string Transactions = "Pages.Transactions";
            public const string Posts = "Pages.Posts";
        }

        public static class Actions
        {
            public const string Create = "Actions.Create";
            public const string View = "Actions.View";
            public const string Edit = "Actions.Edit";
            public const string Delete = "Actions.Delete";
            public const string Lock = "Actions.Lock";
        }
        public static List<string> GetAll()
        {
            return new List<string>
            {
                $"{Pages.Dashboard}.{Actions.View}",

                $"{Pages.Users}.{Actions.View}",
                $"{Pages.Users}.{Actions.Lock}",
                $"{Pages.Users}.{Actions.Delete}",

                $"{Pages.Roles}.{Actions.View}",
                $"{Pages.Roles}.{Actions.Edit}",
                $"{Pages.Roles}.{Actions.Delete}",

                $"{Pages.Wallets}.{Actions.View}",
                $"{Pages.Wallets}.{Actions.Edit}",

                $"{Pages.Transactions}.{Actions.View}",
            };
        }
    }
}