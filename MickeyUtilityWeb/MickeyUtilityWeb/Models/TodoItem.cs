using System;
using System.ComponentModel.DataAnnotations;

namespace MickeyUtilityWeb.Models
{
    public class TodoItem
    {
        public string ID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot be longer than 100 characters")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters")]
        public string Description { get; set; } = string.Empty;

        public DateTime? DueDate { get; set; }

        public bool IsCompleted { get; set; } = false;

        [StringLength(50, ErrorMessage = "Category cannot be longer than 50 characters")]
        public string Category { get; set; } = "Uncategorized";

        public string ParentTaskId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

        public DateTime? DeletedDate { get; set; }

        public TodoItem()
        {
            // Constructor to ensure all fields are properly initialized
            ID = Guid.NewGuid().ToString(); // Generate a unique ID by default
        }

        public void Update()
        {
            UpdatedAt = DateTime.UtcNow;
            LastModifiedDate = DateTime.UtcNow;
        }

        public void MarkAsDeleted()
        {
            IsDeleted = true;
            DeletedDate = DateTime.UtcNow;
            Update();
        }

        // Helper method to get a string representation of dates for Excel
        public string GetFormattedDueDate()
        {
            return DueDate?.ToString("yyyy-MM-ddTHH:mm:ss.fff") ?? string.Empty;
        }

        public string GetFormattedDeletedDate()
        {
            return DeletedDate?.ToString("yyyy-MM-ddTHH:mm:ss.fff") ?? string.Empty;
        }
    }
}