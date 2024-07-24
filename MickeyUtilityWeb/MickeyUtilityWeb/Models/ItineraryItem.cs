using System;
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

        public string TimeString
        {
            get => Time?.ToString() ?? "";
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Time = null;
                }
                else
                {
                    var times = value.Split('-').Select(t => t.Trim()).ToArray();
                    if (times.Length == 2)
                    {
                        Time = new TimeEntry
                        {
                            Start = TimeSpan.TryParse(times[0], out var start) ? start : (TimeSpan?)null,
                            End = TimeSpan.TryParse(times[1], out var end) ? end : (TimeSpan?)null
                        };
                    }
                    else
                    {
                        Time = new TimeEntry
                        {
                            Start = TimeSpan.TryParse(value, out var time) ? time : (TimeSpan?)null,
                            End = null
                        };
                    }
                }
            }
        }
    }
}