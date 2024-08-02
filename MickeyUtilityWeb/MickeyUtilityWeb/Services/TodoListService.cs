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

        public async Task UpdateTodoListInOneDrive(List<TodoItem> todoList)
        {
            try
            {
                var (currentRows, currentColumns, _) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "Task", "IsCompleted", "DueDate", "Category" }
                };

                updateData.AddRange(todoList.Select(item => new object[]
                {
                    item.Task,
                    item.IsCompleted,
                    item.DueDate?.ToString("yyyy-MM-dd") ?? "",
                    item.Category
                }));

                // Pad the data if necessary
                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[currentColumns]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:D{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated todo list in OneDrive");
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
                var excelContent = await _excelApiService.GetFileContent(FILE_ID);

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;
                    var colCount = worksheet.Dimension.Columns;

                    var records = new List<TodoItem>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var item = new TodoItem
                        {
                            Task = worksheet.Cells[row, 1].Value?.ToString(),
                            IsCompleted = bool.Parse(worksheet.Cells[row, 2].Value?.ToString() ?? "false"),
                            DueDate = DateTime.TryParse(worksheet.Cells[row, 3].Value?.ToString(), out var dueDate) ? dueDate : (DateTime?)null,
                            Category = worksheet.Cells[row, 4].Value?.ToString() ?? "Uncategorized"
                        };

                        if (!string.IsNullOrWhiteSpace(item.Task))
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

        public async Task AddTodoItem(TodoItem newItem)
        {
            try
            {
                var currentItems = await GetTodoListFromOneDrive();
                currentItems.Add(newItem);

                var (_, _, rangeAddress) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "Task", "IsCompleted", "DueDate", "Category" }
                };

                updateData.AddRange(currentItems.Select(item => new object[]
                {
                    item.Task,
                    item.IsCompleted,
                    item.DueDate?.ToString("yyyy-MM-dd") ?? "",
                    item.Category
                }));

                string newRangeAddress = $"{WORKSHEET_NAME}!A1:D{updateData.Count}";

                await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, newRangeAddress, updateData);

                _logger.LogInformation($"Successfully added new item: {newItem.Task}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception error adding new todo item");
                throw;
            }
        }

        public async Task DeleteTodoItem(TodoItem itemToDelete)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete todo item: {itemToDelete.Task}");

                var (rowCount, colCount, _) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var excelContent = await _excelApiService.GetFileContent(FILE_ID);

                int rowToDelete = -1;

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];

                    for (int row = 2; row <= rowCount; row++)
                    {
                        if (worksheet.Cells[row, 1].Value?.ToString() == itemToDelete.Task &&
                            bool.Parse(worksheet.Cells[row, 2].Value?.ToString() ?? "false") == itemToDelete.IsCompleted &&
                            DateTime.TryParse(worksheet.Cells[row, 3].Value?.ToString(), out var dueDate) &&
                            dueDate == itemToDelete.DueDate &&
                            worksheet.Cells[row, 4].Value?.ToString() == itemToDelete.Category)
                        {
                            rowToDelete = row;
                            break;
                        }
                    }
                }

                if (rowToDelete == -1)
                {
                    _logger.LogWarning($"Item not found for deletion: {itemToDelete.Task}");
                    return;
                }

                // Delete the specific row
                var deleteRowRange = $"{WORKSHEET_NAME}!A{rowToDelete}:D{rowToDelete}";
                _logger.LogInformation($"Deleting row range: {deleteRowRange}");

                await _excelApiService.DeleteRow(FILE_ID, WORKSHEET_NAME, deleteRowRange);

                _logger.LogInformation($"Successfully deleted item: {itemToDelete.Task}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting todo item: {itemToDelete.Task}");
                throw;
            }
        }
    }
}