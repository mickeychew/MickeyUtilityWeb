using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MickeyUtilityWeb.Models;
using OfficeOpenXml;

namespace MickeyUtilityWeb.Services
{
    public class TodoListService
    {
        private readonly ExcelApiService _excelApiService;
        private readonly ILogger<TodoListService> _logger;
        private const string FILE_ID = "85E9FC7E76F38D5C!s54a1e263a450422582ec249631d712d6";
        private const string WORKSHEET_NAME = "Sheet1";

        public TodoListService(ExcelApiService excelApiService, ILogger<TodoListService> logger)
        {
            _excelApiService = excelApiService;
            _logger = logger;
        }

        public async Task<List<TodoItem>> UpdateTodoListInOneDrive(List<TodoItem> todoList)
        {
            try
            {
                await GetFileContent(); // Ensure we have the latest content before updating

                var (currentRows, _, _) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "Title", "Description", "DueDate", "IsCompleted", "Category", "SubtaskOf", "CreatedAt", "UpdatedAt", "IsDeleted", "LastModifiedDate", "DeletedDate" }
                };

                updateData.AddRange(todoList.Select(item => new object[]
                {
                    item.Title,
                    item.Description,
                    item.DueDate?.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    item.IsCompleted,
                    item.Category,
                    item.SubtaskOf,
                    item.CreatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    item.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    item.IsDeleted,
                    item.LastModifiedDate.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    item.DeletedDate?.ToString("yyyy-MM-ddTHH:mm:sszzz")
                }));

                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[11]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:K{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated todo list in OneDrive");

                // Return the updated list
                return await GetTodoListFromOneDrive();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating todo list in OneDrive");
                throw;
            }
        }

        public async Task<List<TodoItem>> GetTodoListFromOneDrive()
        {
            try
            {
                var excelContent = await GetFileContent();

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;

                    var records = new List<TodoItem>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var item = new TodoItem
                        {
                            Title = worksheet.Cells[row, 1].Value?.ToString(),
                            Description = worksheet.Cells[row, 2].Value?.ToString(),
                            DueDate = DateTime.TryParse(worksheet.Cells[row, 3].Value?.ToString(), out var dueDate) ? dueDate : (DateTime?)null,
                            IsCompleted = bool.Parse(worksheet.Cells[row, 4].Value?.ToString() ?? "false"),
                            Category = worksheet.Cells[row, 5].Value?.ToString() ?? "Uncategorized",
                            SubtaskOf = worksheet.Cells[row, 6].Value?.ToString(),
                            CreatedAt = DateTimeOffset.Parse(worksheet.Cells[row, 7].Value?.ToString() ?? DateTimeOffset.Now.ToString()),
                            UpdatedAt = DateTimeOffset.Parse(worksheet.Cells[row, 8].Value?.ToString() ?? DateTimeOffset.Now.ToString()),
                            IsDeleted = bool.Parse(worksheet.Cells[row, 9].Value?.ToString() ?? "false"),
                            LastModifiedDate = DateTimeOffset.Parse(worksheet.Cells[row, 10].Value?.ToString() ?? DateTimeOffset.Now.ToString()),
                            DeletedDate = DateTime.TryParse(worksheet.Cells[row, 11].Value?.ToString(), out var deletedDate) ? deletedDate : (DateTime?)null
                        };

                        if (!string.IsNullOrWhiteSpace(item.Title))
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

        public async Task<List<TodoItem>> AddTodoItem(TodoItem newItem)
        {
            try
            {
                await GetFileContent(); // Ensure we have the latest content before adding

                var currentItems = await GetTodoListFromOneDrive();
                newItem.CreatedAt = DateTimeOffset.Now;
                newItem.UpdatedAt = DateTimeOffset.Now;
                newItem.LastModifiedDate = DateTimeOffset.Now;
                currentItems.Add(newItem);

                var updatedList = await UpdateTodoListInOneDrive(currentItems);

                _logger.LogInformation($"Successfully added new item: {newItem.Title}");

                return updatedList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception error adding new todo item");
                throw;
            }
        }

        public async Task<List<TodoItem>> DeleteTodoItem(TodoItem itemToDelete)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete todo item: {itemToDelete.Title}");

                await GetFileContent(); // Ensure we have the latest content before deleting

                var currentItems = await GetTodoListFromOneDrive();
                var itemToRemove = currentItems.FirstOrDefault(i => i.Title == itemToDelete.Title && i.CreatedAt == itemToDelete.CreatedAt);

                if (itemToRemove != null)
                {
                    itemToRemove.IsDeleted = true;
                    itemToRemove.DeletedDate = DateTime.Now;
                    itemToRemove.LastModifiedDate = DateTimeOffset.Now;

                    var updatedList = await UpdateTodoListInOneDrive(currentItems);

                    _logger.LogInformation($"Successfully marked item as deleted: {itemToDelete.Title}");

                    return updatedList;
                }
                else
                {
                    _logger.LogWarning($"Item not found for deletion: {itemToDelete.Title}");
                    return currentItems;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting todo item: {itemToDelete.Title}");
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
    }
}