using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MickeyUtilityWeb.Models;
using OfficeOpenXml;

namespace MickeyUtilityWeb.Services
{
    public class ShoppingListService
    {
        private readonly ExcelApiService _excelApiService;
        private readonly ILogger<ShoppingListService> _logger;
        private const string FILE_ID = "85E9FC7E76F38D5C!s75a3662de3b7461482e030e60b5f1ed8"; // Replace with actual file ID
        private const string WORKSHEET_NAME = "Sheet1";

        public ShoppingListService(ExcelApiService excelApiService, ILogger<ShoppingListService> logger)
        {
            _excelApiService = excelApiService;
            _logger = logger;
        }

        public async Task UpdateShoppingListInOneDrive(List<ShoppingItem> shoppingList)
        {
            try
            {
                await GetFileContent();
                var (currentRows, _, _) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "Name", "Quantity", "Category", "IsPurchased", "CreatedAt", "UpdatedAt", "IsDeleted", "LastModifiedDate", "DeletedDate" }
                };

                updateData.AddRange(shoppingList.Select(item => new object[]
                {
                    item.Name,
                    item.Quantity,
                    item.Category,
                    item.IsPurchased,
                    item.CreatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    item.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    item.IsDeleted,
                    item.LastModifiedDate.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    item.DeletedDate?.ToString("yyyy-MM-ddTHH:mm:sszzz")
                }));

                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[9]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:I{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated shopping list in OneDrive");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shopping list in OneDrive");
                throw;
            }
        }
        private async Task<byte[]> GetFileContent()
        {
            try
            {
                return await _excelApiService.GetFileContent(FILE_ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file content from OneDrive");
                throw;
            }
        }
        public async Task<List<ShoppingItem>> GetShoppingListFromOneDrive()
        {
            try
            {
                var excelContent = await _excelApiService.GetFileContent(FILE_ID);

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;

                    var records = new List<ShoppingItem>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var item = new ShoppingItem
                        {
                            Name = worksheet.Cells[row, 1].Value?.ToString(),
                            Quantity = int.Parse(worksheet.Cells[row, 2].Value?.ToString() ?? "0"),
                            Category = worksheet.Cells[row, 3].Value?.ToString() ?? "Uncategorized",
                            IsPurchased = bool.Parse(worksheet.Cells[row, 4].Value?.ToString() ?? "false"),
                            CreatedAt = DateTimeOffset.Parse(worksheet.Cells[row, 5].Value?.ToString() ?? DateTimeOffset.Now.ToString()),
                            UpdatedAt = DateTimeOffset.Parse(worksheet.Cells[row, 6].Value?.ToString() ?? DateTimeOffset.Now.ToString()),
                            IsDeleted = bool.Parse(worksheet.Cells[row, 7].Value?.ToString() ?? "false"),
                            LastModifiedDate = DateTimeOffset.Parse(worksheet.Cells[row, 8].Value?.ToString() ?? DateTimeOffset.Now.ToString()),
                            DeletedDate = DateTime.TryParse(worksheet.Cells[row, 9].Value?.ToString(), out var deletedDate) ? deletedDate : (DateTime?)null
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

        public async Task AddShoppingItem(ShoppingItem newItem)
        {
            try
            {

                var currentItems = await GetShoppingListFromOneDrive();
                newItem.CreatedAt = DateTimeOffset.Now;
                newItem.UpdatedAt = DateTimeOffset.Now;
                newItem.LastModifiedDate = DateTimeOffset.Now;
                currentItems.Add(newItem);

                await UpdateShoppingListInOneDrive(currentItems);

                _logger.LogInformation($"Successfully added new item: {newItem.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception error adding new shopping item");
                throw;
            }
        }

        public async Task DeleteShoppingItem(ShoppingItem itemToDelete)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete shopping item: {itemToDelete.Name}");
                await GetFileContent();
                var currentItems = await GetShoppingListFromOneDrive();
                var itemToRemove = currentItems.FirstOrDefault(i => i.Name == itemToDelete.Name && i.CreatedAt == itemToDelete.CreatedAt);

                if (itemToRemove != null)
                {
                    itemToRemove.IsDeleted = true;
                    itemToRemove.DeletedDate = DateTime.Now;
                    itemToRemove.LastModifiedDate = DateTimeOffset.Now;

                    await UpdateShoppingListInOneDrive(currentItems);

                    _logger.LogInformation($"Successfully marked item as deleted: {itemToDelete.Name}");
                }
                else
                {
                    _logger.LogWarning($"Item not found for deletion: {itemToDelete.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting shopping item: {itemToDelete.Name}");
                throw;
            }
        }
    }
}