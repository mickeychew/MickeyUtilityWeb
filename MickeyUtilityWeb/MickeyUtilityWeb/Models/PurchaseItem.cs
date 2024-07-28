namespace MickeyUtilityWeb.Models
{
    public class PurchaseItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Category { get; set; }
        public bool Purchased { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public DateTime? WarrantyDate { get; set; }
    }
}
