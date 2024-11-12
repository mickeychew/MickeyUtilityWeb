using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MickeyUtilityWeb.Models;
using OfficeOpenXml;

namespace MickeyUtilityWeb.Services
{
    public class PurchaseTrackerService
    {
        private readonly ExcelApiService _excelApiService;
        private readonly FileIdService _fileIdService;
        private readonly ILogger<PurchaseTrackerService> _logger;
        private const string WORKSHEET_NAME = "Sheet1";

        public PurchaseTrackerService(ExcelApiService excelApiService, FileIdService fileIdService, ILogger<PurchaseTrackerService> logger)
        {
            _excelApiService = excelApiService;
            _fileIdService = fileIdService;
            _logger = logger;
        }

        public async Task<List<ExcelListItem>> GetAvailableLists()
        {
            return await _fileIdService.GetFileIdsByService("purchaselist");
        }

        public async Task<List<PurchaseTrackerItem>> UpdatePurchaseListInOneDrive(string key, List<PurchaseTrackerItem> purchaseList)
        {
            try
            {
                string fileId = await _fileIdService.GetFileId(key);
                await GetFileContent(key);

                var (currentRows, _, _) = await _excelApiService.GetCurrentRange(fileId, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "ID", "ProductName", "Category", "ShopName", "ContactPerson", "ContactNumber",
                                 "InvoiceNumber", "OriginalPrice", "DiscountAmount", "DiscountPercentage", "ItemPrice",
                                 "SoldAmount", "RemainingAmount", "PaymentType", "DepositAmount", "TotalPaid",
                                 "PaymentProgress", "DepositPaymentDate", "WarrantyDate", "ExpectedDeliveryDate",
                                 "IsItemReceived", "Remarks", "CreatedAt", "UpdatedAt", "IsDeleted",
                                 "LastModifiedDate", "DeletedDate" }
                };

                updateData.AddRange(purchaseList.Select(item => new object[]
                {
                    item.ID,
                    item.ProductName,
                    item.Category,
                    item.ShopName,
                    item.ContactPerson,
                    item.ContactNumber,
                    item.InvoiceNumber,
                    item.OriginalPrice,
                    item.DiscountAmount,
                    item.DiscountPercentage,
                    item.ItemPrice,
                    item.SoldAmount,
                    item.RemainingAmount,
                    item.PaymentType,
                    item.DepositAmount,
                    item.TotalPaid,
                    item.PaymentProgress,
                    item.DepositPaymentDate?.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                    item.WarrantyDate?.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                    item.ExpectedDeliveryDate?.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                    item.IsItemReceived,
                    item.Remarks,
                    item.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                    item.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                    item.IsDeleted,
                    item.LastModifiedDate.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                    item.DeletedDate?.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                }));

                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[27]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:AA{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(fileId, WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated purchase list in OneDrive");

                return await GetPurchaseListFromOneDrive(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating purchase list in OneDrive");
                throw;
            }
        }

        public async Task<List<PurchaseTrackerItem>> GetPurchaseListFromOneDrive(string key)
        {
            try
            {
                string fileId = await _fileIdService.GetFileId(key);
                var excelContent = await GetFileContent(key);

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;

                    var records = new List<PurchaseTrackerItem>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var item = new PurchaseTrackerItem
                        {
                            ID = worksheet.Cells[row, 1].Value?.ToString(),
                            ProductName = worksheet.Cells[row, 2].Value?.ToString(),
                            Category = worksheet.Cells[row, 3].Value?.ToString(),
                            ShopName = worksheet.Cells[row, 4].Value?.ToString(),
                            ContactPerson = worksheet.Cells[row, 5].Value?.ToString(),
                            ContactNumber = worksheet.Cells[row, 6].Value?.ToString(),
                            InvoiceNumber = worksheet.Cells[row, 7].Value?.ToString(),
                            OriginalPrice = decimal.TryParse(worksheet.Cells[row, 8].Value?.ToString(), out var originalPrice) ? originalPrice : null,
                            DiscountAmount = decimal.TryParse(worksheet.Cells[row, 9].Value?.ToString(), out var discountAmount) ? discountAmount : null,
                            DiscountPercentage = decimal.TryParse(worksheet.Cells[row, 10].Value?.ToString(), out var discountPercentage) ? discountPercentage : null,
                            ItemPrice = decimal.TryParse(worksheet.Cells[row, 11].Value?.ToString(), out var itemPrice) ? itemPrice : null,
                            SoldAmount = decimal.TryParse(worksheet.Cells[row, 12].Value?.ToString(), out var soldAmount) ? soldAmount : null,
                            RemainingAmount = decimal.TryParse(worksheet.Cells[row, 13].Value?.ToString(), out var remainingAmount) ? remainingAmount : null,
                            PaymentType = worksheet.Cells[row, 14].Value?.ToString(),
                            DepositAmount = decimal.TryParse(worksheet.Cells[row, 15].Value?.ToString(), out var depositAmount) ? depositAmount : null,
                            TotalPaid = decimal.TryParse(worksheet.Cells[row, 16].Value?.ToString(), out var totalPaid) ? totalPaid : null,
                            PaymentProgress = worksheet.Cells[row, 17].Value?.ToString(),
                            DepositPaymentDate = DateTime.TryParse(worksheet.Cells[row, 18].Value?.ToString(), out var depositDate) ? depositDate : null,
                            WarrantyDate = DateTime.TryParse(worksheet.Cells[row, 19].Value?.ToString(), out var warrantyDate) ? warrantyDate : null,
                            ExpectedDeliveryDate = DateTime.TryParse(worksheet.Cells[row, 20].Value?.ToString(), out var deliveryDate) ? deliveryDate : null,
                            IsItemReceived = bool.TryParse(worksheet.Cells[row, 21].Value?.ToString(), out var isReceived) && isReceived,
                            Remarks = worksheet.Cells[row, 22].Value?.ToString(),
                            CreatedAt = DateTime.Parse(worksheet.Cells[row, 23].Value?.ToString() ?? DateTime.Now.ToString()),
                            UpdatedAt = DateTime.Parse(worksheet.Cells[row, 24].Value?.ToString() ?? DateTime.Now.ToString()),
                            IsDeleted = bool.TryParse(worksheet.Cells[row, 25].Value?.ToString(), out var isDeleted) && isDeleted,
                            LastModifiedDate = DateTime.Parse(worksheet.Cells[row, 26].Value?.ToString() ?? DateTime.Now.ToString()),
                            DeletedDate = DateTime.TryParse(worksheet.Cells[row, 27].Value?.ToString(), out var deletedDate) ? deletedDate : null
                        };

                        if (!string.IsNullOrWhiteSpace(item.ProductName))
                        {
                            item.CalculateValues();
                            records.Add(item);
                        }
                    }

                    return records;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from OneDrive");
                throw;
            }
        }

        public async Task<List<PurchaseTrackerItem>> AddPurchaseItem(string key, PurchaseTrackerItem newItem)
        {
            try
            {
                await GetFileContent(key);
                var currentItems = await GetPurchaseListFromOneDrive(key);
                newItem.ID = GenerateNewId(currentItems);
                newItem.CreatedAt = DateTime.Now;
                newItem.UpdatedAt = DateTime.Now;
                newItem.LastModifiedDate = DateTime.Now;
                currentItems.Add(newItem);

                var updatedList = await UpdatePurchaseListInOneDrive(key, currentItems);

                _logger.LogInformation($"Successfully added new item: {newItem.ProductName}");

                return updatedList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception error adding new purchase item");
                throw;
            }
        }

        public async Task<List<PurchaseTrackerItem>> DeletePurchaseItem(string key, PurchaseTrackerItem itemToDelete)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete purchase item: {itemToDelete.ProductName}");

                await GetFileContent(key);

                var currentItems = await GetPurchaseListFromOneDrive(key);
                var itemToRemove = currentItems.FirstOrDefault(i => i.ID == itemToDelete.ID);

                if (itemToRemove != null)
                {
                    itemToRemove.IsDeleted = true;
                    itemToRemove.DeletedDate = DateTime.Now;
                    itemToRemove.LastModifiedDate = DateTime.Now;

                    var updatedList = await UpdatePurchaseListInOneDrive(key, currentItems);

                    _logger.LogInformation($"Successfully marked item as deleted: {itemToDelete.ProductName}");

                    return updatedList;
                }
                else
                {
                    _logger.LogWarning($"Item not found for deletion: {itemToDelete.ProductName}");
                    return currentItems;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting purchase item: {itemToDelete.ProductName}");
                throw;
            }
        }

        private async Task<byte[]> GetFileContent(string key)
        {
            try
            {
                string fileId = await _fileIdService.GetFileId(key);
                return await _excelApiService.GetFileContent(fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file content from OneDrive");
                throw;
            }
        }

        private string GenerateNewId(List<PurchaseTrackerItem> currentItems)
        {
            string prefix = "PRMC";
            int maxId = currentItems
                .Where(item => item.ID.StartsWith(prefix))
                .Select(item => int.TryParse(item.ID.Substring(4), out int id) ? id : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"{prefix}{maxId + 1}";
        }
    }
}