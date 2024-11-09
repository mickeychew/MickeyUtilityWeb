namespace MickeyUtilityWeb.Models
{
    public class PurchaseTrackerItem
    {
        public string ID { get; set; }
        public string ProductName { get; set; }
        public string Category { get; set; }
        public string ShopName { get; set; }
        public string ContactPerson { get; set; }
        public string ContactNumber { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal? OriginalPrice { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public decimal? ItemPrice { get; set; }
        public decimal? SoldAmount { get; set; }
        public decimal? RemainingAmount { get; set; }
        public string PaymentType { get; set; }
        public decimal? DepositAmount { get; set; }
        public decimal? TotalPaid { get; set; }
        public string PaymentProgress { get; set; }
        public DateTime? DepositPaymentDate { get; set; }
        public DateTime? WarrantyDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public bool IsItemReceived { get; set; }
        public string Remarks { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }

        public bool HasDiscount => (DiscountAmount.HasValue && DiscountAmount.Value > 0) ||
                                 (DiscountPercentage.HasValue && DiscountPercentage.Value > 0);

        public void CalculateValues()
        {
            if (OriginalPrice.HasValue)
            {
                if (DiscountPercentage.HasValue)
                {
                    ItemPrice = OriginalPrice.Value * (1 - DiscountPercentage.Value / 100);
                }
                else if (DiscountAmount.HasValue)
                {
                    ItemPrice = OriginalPrice.Value - DiscountAmount.Value;
                }
                else
                {
                    ItemPrice = OriginalPrice.Value;
                }

                if (ItemPrice.HasValue && ItemPrice.Value > 0)
                {
                    decimal totalPaidAmount = (TotalPaid ?? 0) + (DepositAmount ?? 0);

                    if (PaymentProgress == "Free")
                    {
                        TotalPaid = 0;
                        DepositAmount = 0;
                        RemainingAmount = 0;
                    }
                    else if (totalPaidAmount >= ItemPrice.Value)
                    {
                        PaymentProgress = "100%";
                        RemainingAmount = 0;
                    }
                    else if (totalPaidAmount > 0)
                    {
                        decimal progress = (totalPaidAmount / ItemPrice.Value) * 100;
                        PaymentProgress = $"{progress:F2}%";
                        RemainingAmount = ItemPrice.Value - totalPaidAmount;
                    }
                    else
                    {
                        PaymentProgress = "0%";
                        RemainingAmount = ItemPrice.Value;
                    }
                }

                if (SoldAmount.HasValue && SoldAmount.Value > 0)
                {
                    RemainingAmount = ItemPrice.Value - SoldAmount.Value;
                }
            }
        }

        public void SetPaymentType(string type)
        {
            switch (type.ToLower())
            {
                case "free":
                    PaymentProgress = "Free";
                    TotalPaid = 0;
                    DepositAmount = 0;
                    SoldAmount = 0;
                    break;
                case "full":
                    if (ItemPrice.HasValue)
                    {
                        TotalPaid = ItemPrice.Value;
                        DepositAmount = 0;
                        SoldAmount = 0;
                        PaymentProgress = "100%";
                    }
                    break;
                case "sold":
                    if (ItemPrice.HasValue)
                    {
                        SoldAmount = ItemPrice.Value;
                        PaymentProgress = "Sold";
                        TotalPaid = ItemPrice.Value;
                        DepositAmount = 0;
                    }
                    break;
                case "partial":
                    TotalPaid = 0;
                    DepositAmount = 0;
                    SoldAmount = 0;
                    PaymentProgress = "0%";
                    break;
                default:
                    if (ItemPrice.HasValue)
                    {
                        PaymentProgress = "0%";
                        TotalPaid = 0;
                        DepositAmount = 0;
                        SoldAmount = 0;
                    }
                    break;
            }
            CalculateValues();
        }
    }
}