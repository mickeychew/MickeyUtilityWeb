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
        private readonly FileIdService _fileIdService;
        private readonly ILogger<ShoppingListService> _logger;
        private const string WORKSHEET_NAME = "Sheet1";

        public ShoppingListService(ExcelApiService excelApiService, FileIdService fileIdService, ILogger<ShoppingListService> logger)
        {
            _excelApiService = excelApiService;
            _fileIdService = fileIdService;
            _logger = logger;
        }

        public async Task<List<ExcelListItem>> GetAvailableLists()
        {
            return await _fileIdService.GetFileIdsByService("ShoppingList");
        }

        public async Task<List<ShoppingItem>> UpdateShoppingListInOneDrive(string key, List<ShoppingItem> shoppingList)
        {
            try
            {
                string fileId = await _fileIdService.GetFileId(key);
                await GetFileContent(key);

                var (currentRows, _, _) = await _excelApiService.GetCurrentRange(fileId, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "ID", "Name", "Quantity", "Category", "IsPurchased", "CreatedAt", "UpdatedAt", "IsDeleted", "LastModifiedDate", "DeletedDate" }
                };

                updateData.AddRange(shoppingList.Select(item => new object[]
                {
                    item.ID,
                    item.Name,
                    item.Quantity,
                    item.Category,
                    item.IsPurchased,
                    item.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                    item.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                    item.IsDeleted,
                    item.LastModifiedDate.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                    item.DeletedDate?.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                }));

                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[10]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:J{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(fileId, WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated shopping list in OneDrive");

                return await GetShoppingListFromOneDrive(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shopping list in OneDrive");
                throw;
            }
        }

        public async Task<List<ShoppingItem>> GetShoppingListFromOneDrive(string key)
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

                    var records = new List<ShoppingItem>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var item = new ShoppingItem
                        {
                            ID = worksheet.Cells[row, 1].Value?.ToString(),
                            Name = worksheet.Cells[row, 2].Value?.ToString(),
                            Quantity = int.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? "0"),
                            Category = worksheet.Cells[row, 4].Value?.ToString() ?? "Uncategorized",
                            IsPurchased = bool.Parse(worksheet.Cells[row, 5].Value?.ToString() ?? "false"),
                            CreatedAt = DateTime.Parse(worksheet.Cells[row, 6].Value?.ToString() ?? DateTime.Now.ToString("MM/dd/yyyy HH:mm")),
                            UpdatedAt = DateTime.Parse(worksheet.Cells[row, 7].Value?.ToString() ?? DateTime.Now.ToString("MM/dd/yyyy HH:mm")),
                            IsDeleted = bool.Parse(worksheet.Cells[row, 8].Value?.ToString() ?? "false"),
                            LastModifiedDate = DateTime.Parse(worksheet.Cells[row, 9].Value?.ToString() ?? DateTime.Now.ToString("MM/dd/yyyy HH:mm")),
                            DeletedDate = DateTime.TryParse(worksheet.Cells[row, 10].Value?.ToString(), out var deletedDate) ? deletedDate : (DateTime?)null
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

        public async Task<List<ShoppingItem>> AddShoppingItem(string key, ShoppingItem newItem)
        {
            try
            {
                await GetFileContent(key);
                var currentItems = await GetShoppingListFromOneDrive(key);
                newItem.ID = GenerateNewId(currentItems);
                newItem.CreatedAt = DateTime.Now;
                newItem.UpdatedAt = DateTime.Now;
                newItem.LastModifiedDate = DateTime.Now;
                currentItems.Add(newItem);

                var updatedList = await UpdateShoppingListInOneDrive(key, currentItems);

                _logger.LogInformation($"Successfully added new item: {newItem.Name}");

                return updatedList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception error adding new shopping item");
                throw;
            }
        }

        public async Task<List<ShoppingItem>> DeleteShoppingItem(string key, ShoppingItem itemToDelete)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete shopping item: {itemToDelete.Name}");

                await GetFileContent(key);

                var currentItems = await GetShoppingListFromOneDrive(key);
                var itemToRemove = currentItems.FirstOrDefault(i => i.ID == itemToDelete.ID);

                if (itemToRemove != null)
                {
                    itemToRemove.IsDeleted = true;
                    itemToRemove.DeletedDate = DateTime.Now;
                    itemToRemove.LastModifiedDate = DateTime.Now;

                    var updatedList = await UpdateShoppingListInOneDrive(key, currentItems);

                    _logger.LogInformation($"Successfully marked item as deleted: {itemToDelete.Name}");

                    return updatedList;
                }
                else
                {
                    _logger.LogWarning($"Item not found for deletion: {itemToDelete.Name}");
                    return currentItems;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting shopping item: {itemToDelete.Name}");
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

        private string GenerateNewId(List<ShoppingItem> currentItems)
        {
            string prefix = "MCSL";
            int maxId = currentItems
                .Where(item => item.ID.StartsWith(prefix))
                .Select(item => int.TryParse(item.ID.Substring(4), out int id) ? id : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"{prefix}{maxId + 1}";
        }
    }
}