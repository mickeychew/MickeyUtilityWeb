namespace MickeyUtilityWeb.Models
{
    public class TodoItem
    {
        public string ID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTimeOffset? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public string Category { get; set; }
        public string ParentTaskId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTimeOffset LastModifiedDate { get; set; }
        public DateTimeOffset? DeletedDate { get; set; }
    }
}
