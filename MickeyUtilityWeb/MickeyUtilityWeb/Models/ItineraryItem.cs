using System;
using MickeyUtilityWeb.Services;

namespace MickeyUtilityWeb.Models
{

    public class ItineraryItem
    {
        public string ID { get; set; }
        public bool IsChecked { get; set; }
        public int Day { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Activity { get; set; }
        public string Icon { get; set; }
        public string Location { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }

    }
    //public class TimeEntry
    //{
    //    public TimeSpan? Start { get; set; }
    //    public TimeSpan? End { get; set; }

    //    public override string ToString()
    //    {
    //        if (End.HasValue)
    //        {
    //            return $"{FormatTime(Start)} - {FormatTime(End)}";
    //        }
    //        else
    //        {
    //            return FormatTime(Start);
    //        }
    //    }

    //    private string FormatTime(TimeSpan? time)
    //    {
    //        return time?.ToString("hh\\:mm") ?? "";
    //    }
    //}

    //public class ItineraryItem
    //{
    //    public bool IsChecked { get; set; }
    //    public string Day { get; set; }
    //    public DateTime Date { get; set; }
    //    public TimeEntry Time { get; set; }
    //    public string Activity { get; set; }
    //    public string Icon { get; set; }
    //    public string Location { get; set; }
    //    public string TimeString
    //    {
    //        get => Time?.ToString() ?? "";
    //        set
    //        {
    //            if (string.IsNullOrWhiteSpace(value))
    //            {
    //                Time = null;
    //            }
    //            else
    //            {
    //                var times = value.Split('-').Select(t => t.Trim()).ToArray();
    //                if (times.Length == 2)
    //                {
    //                    Time = new TimeEntry
    //                    {
    //                        Start = TimeSpan.TryParse(times[0], out var start) ? start : (TimeSpan?)null,
    //                        End = TimeSpan.TryParse(times[1], out var end) ? end : (TimeSpan?)null
    //                    };
    //                }
    //                else
    //                {
    //                    Time = new TimeEntry
    //                    {
    //                        Start = TimeSpan.TryParse(value, out var time) ? time : (TimeSpan?)null,
    //                        End = null
    //                    };
    //                }
    //            }
    //        }
    //    }
    //}
}
     