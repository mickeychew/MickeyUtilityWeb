namespace MickeyUtilityWeb.Models
{
    public class TodoItem
    {
        public string Task { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
