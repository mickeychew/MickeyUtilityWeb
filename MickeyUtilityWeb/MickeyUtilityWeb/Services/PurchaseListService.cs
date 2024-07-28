using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MickeyUtilityWeb.Models;
using OfficeOpenXml;

namespace MickeyUtilityWeb.Services
{
    public class PurchaseListService
    {
        private readonly ExcelApiService _excelApiService;
        private readonly ILogger<PurchaseListService> _logger;
        private const string FILE_ID = "85E9FC7E76F38D5C!s2ce1d06cee4b48d891c5afaea5baf7fd";
        private const string WORKSHEET_NAME = "Sheet1";

        public PurchaseListService(ExcelApiService excelApiService, ILogger<PurchaseListService> logger)
        {
            _excelApiService = excelApiService;
            _logger = logger;
        }

        public async Task UpdatePurchaseListInOneDrive(List<PurchaseItem> purchaseList)
        {
            try
            {
                var (currentRows, currentColumns, _) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "Name", "Price", "Quantity", "Category", "Purchased", "PurchaseDate", "WarrantyDate" }
                };

                updateData.AddRange(purchaseList.Select(item => new object[]
                {
                    item.Name,
                    item.Price,
                    item.Quantity,
                    item.Category,
                    item.Purchased,
                    item.PurchaseDate?.ToString("yyyy-MM-dd") ?? "",
                    item.WarrantyDate?.ToString("yyyy-MM-dd") ?? ""
                }));

                // Pad the data if necessary
                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[currentColumns]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:G{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated purchase list in OneDrive");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating purchase list in OneDrive");
                throw;
            }
        }

        public async Task<List<PurchaseItem>> GetPurchaseListFromOneDrive()
        {
            try
            {
                var excelContent = await _excelApiService.GetFileContent(FILE_ID);

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;
                    var colCount = worksheet.Dimension.Columns;

                    var records = new List<PurchaseItem>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var item = new PurchaseItem
                        {
                            Name = worksheet.Cells[row, 1].Value?.ToString(),
                            Price = decimal.Parse(worksheet.Cells[row, 2].Value?.ToString() ?? "0"),
                            Quantity = int.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? "0"),
                            Category = worksheet.Cells[row, 4].Value?.ToString(),
                            Purchased = bool.Parse(worksheet.Cells[row, 5].Value?.ToString() ?? "false"),
                            PurchaseDate = DateTime.TryParse(worksheet.Cells[row, 6].Value?.ToString(), out var purchaseDate) ? purchaseDate : (DateTime?)null,
                            WarrantyDate = DateTime.TryParse(worksheet.Cells[row, 7].Value?.ToString(), out var warrantyDate) ? warrantyDate : (DateTime?)null
                        };

                        if (!string.IsNullOrWhiteSpace(item.Name))
                        {
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

        public async Task AddPurchaseItem(PurchaseItem newItem)
        {
            try
            {
                var currentItems = await GetPurchaseListFromOneDrive();
                currentItems.Add(newItem);

                var (_, _, rangeAddress) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "Name", "Price", "Quantity", "Category", "Purchased", "PurchaseDate", "WarrantyDate" }
                };

                updateData.AddRange(currentItems.Select(item => new object[]
                {
                    item.Name,
                    item.Price,
                    item.Quantity,
                    item.Category,
                    item.Purchased,
                    item.PurchaseDate?.ToString("yyyy-MM-dd") ?? "",
                    item.WarrantyDate?.ToString("yyyy-MM-dd") ?? ""
                }));

                string newRangeAddress = $"{WORKSHEET_NAME}!A1:G{updateData.Count}";

                await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, newRangeAddress, updateData);

                _logger.LogInformation($"Successfully added new item: {newItem.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new purchase item");
                throw;
            }
        }

        public async Task DeletePurchaseItem(PurchaseItem itemToDelete)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete purchase item: {itemToDelete.Name}");

                var (rowCount, colCount, _) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var excelContent = await _excelApiService.GetFileContent(FILE_ID);

                int rowToDelete = -1;

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];

                    for (int row = 2; row <= rowCount; row++)
                    {
                        if (worksheet.Cells[row, 1].Value?.ToString() == itemToDelete.Name &&
                            decimal.Parse(worksheet.Cells[row, 2].Value?.ToString() ?? "0") == itemToDelete.Price &&
                            int.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? "0") == itemToDelete.Quantity &&
                            worksheet.Cells[row, 4].Value?.ToString() == itemToDelete.Category)
                        {
                            rowToDelete = row;
                            break;
                        }
                    }
                }

                if (rowToDelete == -1)
                {
                    _logger.LogWarning($"Item not found for deletion: {itemToDelete.Name}");
                    return;
                }

                // Delete the specific row
                var deleteRowRange = $"{WORKSHEET_NAME}!A{rowToDelete}:G{rowToDelete}";
                _logger.LogInformation($"Deleting row range: {deleteRowRange}");

                await _excelApiService.DeleteRow(FILE_ID, WORKSHEET_NAME, deleteRowRange);

                _logger.LogInformation($"Successfully deleted item: {itemToDelete.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting purchase item: {itemToDelete.Name}");
                throw;
            }
        }
    }
}