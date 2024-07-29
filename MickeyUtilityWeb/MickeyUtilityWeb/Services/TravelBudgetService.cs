using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MickeyUtilityWeb.Models;
using OfficeOpenXml;

namespace MickeyUtilityWeb.Services
{
    public class TravelBudgetService
    {
        private readonly ExcelApiService _excelApiService;
        private readonly ILogger<TravelBudgetService> _logger;
        private const string FILE_ID = "85E9FC7E76F38D5C!sfed3bfe619584402abb7874f99497381";
        private const string ITEMS_WORKSHEET_NAME = "Sheet1";
        private const string BUDGET_WORKSHEET_NAME = "Sheet2";

        public TravelBudgetService(ExcelApiService excelApiService, ILogger<TravelBudgetService> logger)
        {
            _excelApiService = excelApiService;
            _logger = logger;
        }

        public async Task UpdateTravelBudgetInOneDrive(List<TravelBudgetItem> travelBudgetList)
        {
            try
            {
                var (currentRows, currentColumns, _) = await _excelApiService.GetCurrentRange(FILE_ID, ITEMS_WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "Name", "Category", "Price", "Date", "Shop" }
                };

                updateData.AddRange(travelBudgetList.Select(item => new object[]
                {
                    item.Name,
                    item.Category,
                    item.Price,
                    item.Date.ToString("yyyy-MM-dd"),
                    item.Shop
                }));

                // Pad the data if necessary
                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[currentColumns]);
                }

                string rangeAddress = $"{ITEMS_WORKSHEET_NAME}!A1:E{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(FILE_ID, ITEMS_WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated travel budget items in OneDrive");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating travel budget items in OneDrive");
                throw;
            }
        }

        public async Task<(List<TravelBudgetItem> Items, decimal Budget)> GetTravelBudgetFromOneDrive()
        {
            try
            {
                var excelContent = await _excelApiService.GetFileContent(FILE_ID);

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    // Read budget from Sheet2
                    var budgetWorksheet = package.Workbook.Worksheets[BUDGET_WORKSHEET_NAME];
                    decimal budget = 0;
                    if (decimal.TryParse(budgetWorksheet.Cells["A1"].Value?.ToString(), out decimal parsedBudget))
                    {
                        budget = parsedBudget;
                    }

                    // Read items from Sheet1
                    var itemsWorksheet = package.Workbook.Worksheets[ITEMS_WORKSHEET_NAME];
                    var rowCount = itemsWorksheet.Dimension.Rows;
                    var records = new List<TravelBudgetItem>();

                    for (int row = 2; row <= rowCount; row++) // Start from row 2 to skip header
                    {
                        var item = new TravelBudgetItem
                        {
                            Name = itemsWorksheet.Cells[row, 1].Value?.ToString(),
                            Category = itemsWorksheet.Cells[row, 2].Value?.ToString(),
                            Price = decimal.Parse(itemsWorksheet.Cells[row, 3].Value?.ToString() ?? "0"),
                            Date = DateTime.Parse(itemsWorksheet.Cells[row, 4].Value?.ToString() ?? DateTime.Now.ToString()),
                            Shop = itemsWorksheet.Cells[row, 5].Value?.ToString()
                        };

                        if (!string.IsNullOrWhiteSpace(item.Name))
                        {
                            records.Add(item);
                        }
                    }

                    return (records, budget);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading travel budget from OneDrive");
                throw;
            }
        }

        public async Task AddTravelBudgetItem(TravelBudgetItem newItem)
        {
            try
            {
                var (currentItems, _) = await GetTravelBudgetFromOneDrive();
                currentItems.Add(newItem);

                var (_, _, rangeAddress) = await _excelApiService.GetCurrentRange(FILE_ID, ITEMS_WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "Name", "Category", "Price", "Date", "Shop" } // Headers in row 1
                };

                updateData.AddRange(currentItems.Select(item => new object[]
                {
                    item.Name,
                    item.Category,
                    item.Price,
                    item.Date.ToString("yyyy-MM-dd"),
                    item.Shop
                }));

                string newRangeAddress = $"{ITEMS_WORKSHEET_NAME}!A1:E{updateData.Count}";

                await _excelApiService.UpdateRange(FILE_ID, ITEMS_WORKSHEET_NAME, newRangeAddress, updateData);

                _logger.LogInformation($"Successfully added new item: {newItem.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new travel budget item");
                throw;
            }
        }

        public async Task DeleteTravelBudgetItem(TravelBudgetItem itemToDelete)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete travel budget item: {itemToDelete.Name}");

                var (rowCount, colCount, _) = await _excelApiService.GetCurrentRange(FILE_ID, ITEMS_WORKSHEET_NAME);

                var excelContent = await _excelApiService.GetFileContent(FILE_ID);

                int rowToDelete = -1;

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[ITEMS_WORKSHEET_NAME];

                    for (int row = 2; row <= rowCount; row++) // Start from row 2 to skip header
                    {
                        if (worksheet.Cells[row, 1].Value?.ToString() == itemToDelete.Name &&
                            worksheet.Cells[row, 2].Value?.ToString() == itemToDelete.Category &&
                            decimal.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? "0") == itemToDelete.Price &&
                            DateTime.Parse(worksheet.Cells[row, 4].Value?.ToString() ?? DateTime.Now.ToString()) == itemToDelete.Date &&
                            worksheet.Cells[row, 5].Value?.ToString() == itemToDelete.Shop)
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
                var deleteRowRange = $"{ITEMS_WORKSHEET_NAME}!A{rowToDelete}:E{rowToDelete}";
                _logger.LogInformation($"Deleting row range: {deleteRowRange}");

                await _excelApiService.DeleteRow(FILE_ID, ITEMS_WORKSHEET_NAME, deleteRowRange);

                _logger.LogInformation($"Successfully deleted item: {itemToDelete.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting travel budget item: {itemToDelete.Name}");
                throw;
            }
        }

        public async Task UpdateBudget(decimal newBudget)
        {
            try
            {
                var updateData = new List<object[]>
                {
                    new object[] { newBudget }
                };

                string rangeAddress = $"{BUDGET_WORKSHEET_NAME}!A1";

                await _excelApiService.UpdateRange(FILE_ID, BUDGET_WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation($"Successfully updated budget to: {newBudget}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating budget");
                throw;
            }
        }
    }
}