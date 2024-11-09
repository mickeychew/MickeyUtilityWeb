namespace MickeyUtilityWeb.Services.SharedServices
{

        public class EmojiIconService
        {
            private readonly Dictionary<string, (string Emoji, string DisplayName)> _icons;

            public EmojiIconService()
            {
                _icons = new Dictionary<string, (string Emoji, string DisplayName)>
        {
            { "Kitchen", ("🍳", "Kitchen Items") },
            { "Bedroom", ("🛏️", "Bedroom Items") },
            { "LivingRoom", ("🛋️", "Living Room") },
            { "Bathroom", ("🚿", "Bathroom Items") },
            { "Lighting", ("💡", "Lighting") },
            { "Flooring", ("🏗️", "Flooring") },
            { "Painting", ("🎨", "Painting") },
            { "Plumbing", ("🔧", "Plumbing") },
            { "Electrical", ("⚡", "Electrical") },
            { "Storage", ("📦", "Storage") },
            { "Ceiling", ("🔝", "Ceiling") },
            { "Wall", ("🧱", "Wall") },
            { "Door", ("🚪", "Door") },
            { "Window", ("🪟", "Window") },
            { "Others", ("📌", "Others") }
        };
            }

            public (string Emoji, string DisplayName) GetIcon(string category)
            {
                if (string.IsNullOrEmpty(category) || !_icons.ContainsKey(category))
                    return ("📌", "Others");

                return _icons[category];
            }

            public Dictionary<string, (string Emoji, string DisplayName)> GetIcons()
            {
                return _icons;
            }

            public string GetCategoryColor(string category)
            {
                if (string.IsNullOrEmpty(category))
                    return "#808080";

                int hash = category.GetHashCode();
                byte r = (byte)(hash & 255);
                byte g = (byte)((hash >> 8) & 255);
                byte b = (byte)((hash >> 16) & 255);
                return $"#{r:X2}{g:X2}{b:X2}";
            }
        }
    
}
