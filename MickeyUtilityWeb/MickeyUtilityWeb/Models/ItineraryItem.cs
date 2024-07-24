using MickeyUtilityWeb.Services;

namespace MickeyUtilityWeb.Models
{
    public class ItineraryItem
    {
        public bool IsChecked { get; set; }
        public string Day { get; set; }
        public DateTime Date { get; set; }
        public TimeEntry Time { get; set; }
        public string Activity { get; set; }
        public string Icon { get; set; }
        public string Location { get; set; }
    }
}
