using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MickeyUtilityWeb.Models;

namespace MickeyUtilityWeb.Services
{
    public class ItineraryTestDataService
    {
        public async Task<List<ItineraryItem>> GetItineraryItems()
        {
            // Simulate async operation
            await Task.Delay(500);

            return new List<ItineraryItem>
            {
                new ItineraryItem { Day = "Day 1", Date = DateTime.Today, TimeString = "08:15", Activity = "Leave house", Icon = "home" },
                new ItineraryItem { Day = "Day 1", Date = DateTime.Today, TimeString = "09:45", Activity = "Reach airport", Icon = "plane" },
                new ItineraryItem { Day = "Day 1", Date = DateTime.Today, TimeString = "11:45 - 13:00", Activity = "Flight", Icon = "plane" },
                new ItineraryItem { Day = "Day 1", Date = DateTime.Today, TimeString = "13:00 - 15:00", Activity = "Lunch at Changi Airport", Icon = "utensils", Location = "Jewel Changi Airport" },
                new ItineraryItem { Day = "Day 1", Date = DateTime.Today, TimeString = "15:00 - 17:00", Activity = "Check in at Hotel", Icon = "hotel", Location = "Marina Bay Sands" },
                new ItineraryItem { Day = "Day 2", Date = DateTime.Today.AddDays(1), TimeString = "09:00 - 11:00", Activity = "Visit Gardens by the Bay", Icon = "camera", Location = "Gardens by the Bay" },
                new ItineraryItem { Day = "Day 2", Date = DateTime.Today.AddDays(1), TimeString = "12:00 - 14:00", Activity = "Lunch at Lau Pa Sat", Icon = "utensils", Location = "Lau Pa Sat" },
                new ItineraryItem { Day = "Day 2", Date = DateTime.Today.AddDays(1), TimeString = "15:00 - 18:00", Activity = "Shopping at Orchard Road", Icon = "shopping-cart", Location = "Orchard Road" },
                new ItineraryItem { Day = "Day 3", Date = DateTime.Today.AddDays(2), TimeString = "10:00 - 13:00", Activity = "Visit Singapore Zoo", Icon = "camera", Location = "Singapore Zoo" },
                new ItineraryItem { Day = "Day 3", Date = DateTime.Today.AddDays(2), TimeString = "14:00 - 16:00", Activity = "Explore Chinatown", Icon = "camera", Location = "Chinatown" },
                new ItineraryItem { Day = "Day 3", Date = DateTime.Today.AddDays(2), TimeString = "18:00 - 20:00", Activity = "Dinner at Maxwell Food Centre", Icon = "utensils", Location = "Maxwell Food Centre" }
            };
        }
    }
}