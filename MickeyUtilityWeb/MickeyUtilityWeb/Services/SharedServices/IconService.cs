using System.Collections.Generic;

namespace MickeyUtilityWeb.Services
{
    public class IconService
    {
        public Dictionary<string, string> GetIcons()
        {
            return new Dictionary<string, string>
            {
                {"icon-home", "Home"},
                {"icon-plane", "Plane"},
                {"icon-utensils", "Food"},
                {"icon-hotel", "Hotel"},
                {"icon-coffee", "Coffee"},
                {"icon-camera", "Camera"},
                {"icon-sun", "Sun"},
                {"icon-car", "Car"},
                {"icon-train", "Train"},
                {"icon-bus", "Bus"},
                {"icon-ship", "Ship"},
                {"icon-bicycle", "Bicycle"},
                {"icon-walking", "Walking"},
                {"icon-shopping-cart", "Shopping"},
                {"icon-museum", "Museum"},
                {"icon-monument", "Monument"},
                {"icon-beach", "Beach"},
                {"icon-mountain", "Mountain"},
                {"icon-park", "Park"},
                {"icon-restaurant", "Restaurant"},
                {"icon-bar", "Bar"},
                {"icon-theater", "Theater"},
                {"icon-movie", "Movie"},
                {"icon-music", "Music"},
                {"icon-swimming", "Swimming"},
                {"icon-gym", "Gym"},
                {"icon-spa", "Spa"},
                {"icon-library", "Library"},
                {"icon-university", "University"},
                {"icon-hospital", "Hospital"}
            };
        }
    }
}