namespace MickeyUtilityWeb.Models
{
    public class TodoItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public string Category { get; set; }
        public string SubtaskOf { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTimeOffset LastModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
    }
}
