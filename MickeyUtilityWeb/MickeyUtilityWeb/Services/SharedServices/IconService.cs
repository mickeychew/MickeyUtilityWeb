using System.Collections.Generic;

namespace MickeyUtilityWeb.Services
{
    public class IconService
    {
        private readonly Dictionary<string, string> _icons;

        public IconService()
        {
            _icons = new Dictionary<string, string>
            {
                {"home", "Home"},
                {"plane", "Plane"},
                {"utensils", "Food"},
                {"hotel", "Hotel"},
                {"coffee", "Coffee"},
                {"camera", "Camera"},
                {"sun", "Sun"},
                {"car", "Car"},
                {"train", "Train"},
                {"bus", "Bus"},
                {"ship", "Ship"},
                {"bicycle", "Bicycle"},
                {"walking", "Walking"},
                {"shopping-cart", "Shopping"},
                {"museum", "Museum"},
                {"monument", "Monument"},
                {"beach", "Beach"},
                {"mountain", "Mountain"},
                {"park", "Park"},
                {"restaurant", "Restaurant"},
                {"bar", "Bar"},
                {"theater", "Theater"},
                {"movie", "Movie"},
                {"music", "Music"},
                {"swimming", "Swimming"},
                {"gym", "Gym"},
                {"spa", "Spa"},
                {"library", "Library"},
                {"university", "University"},
                {"hospital", "Hospital"}
            };
        }

        public Dictionary<string, string> GetIcons()
        {
            return _icons;
        }

        public string GetIconClass(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return "icon-default";
            }

            category = category.ToLower().Trim();

            return _icons.ContainsKey(category) ? $"icon-{category}" : "icon-default";
        }
    }
}