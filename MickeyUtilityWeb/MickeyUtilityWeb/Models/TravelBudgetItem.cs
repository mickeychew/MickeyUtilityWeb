using System.ComponentModel.DataAnnotations;

namespace MickeyUtilityWeb.Models
{
    public class TravelBudgetItem
    {
    
        public string Name { get; set; }


        public string Category { get; set; }

  
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }


        public DateTime Date { get; set; }


        public string Shop { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }
    }
}
