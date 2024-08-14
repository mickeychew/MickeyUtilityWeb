namespace MickeyUtilityWeb.Models
{
    public class ShoppingItem
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public string Category { get; set; }
        public bool IsPurchased { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTimeOffset LastModifiedDate { get; set; }
        public DateTimeOffset? DeletedDate { get; set; }
    }
}
