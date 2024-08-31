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
        private readonly FileIdService _fileIdService;
        private readonly ILogger<TodoListService> _logger;
        private const string WORKSHEET_NAME = "Sheet1";

        public TodoListService(ExcelApiService excelApiService, FileIdService fileIdService, ILogger<TodoListService> logger)
        {
            _excelApiService = excelApiService;
            _fileIdService = fileIdService;
            _logger = logger;
        }

        public async Task<List<ExcelListItem>> GetAvailableLists()
        {
            return await _fileIdService.GetFileIdsByService("TodoList");
        }

        public async Task<List<TodoItem>> UpdateTodoListInOneDrive(string key, List<TodoItem> todoList)
        {
            try
            {
                string fileId = await _fileIdService.GetFileId(key);
                await GetFileContent(key); // Ensure we have the latest content before updating

                var (currentRows, _, _) = await _excelApiService.GetCurrentRange(fileId, WORKSHEET_NAME);

                var updateData = new List<object[]>
            {
                new object[] { "ID", "Title", "Description", "DueDate", "IsCompleted", "Category", "ParentTaskId", "CreatedAt", "UpdatedAt", "IsDeleted", "LastModifiedDate", "DeletedDate" }
            };

                updateData.AddRange(todoList.Select(item => new object[]
                {
                item.ID,
                item.Title,
                item.Description,
                item.GetFormattedDueDate(),
                item.IsCompleted,
                item.Category,
                item.ParentTaskId,
                item.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                item.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                item.IsDeleted,
                item.LastModifiedDate.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                item.GetFormattedDeletedDate()
                }));

                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[12]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:L{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(fileId, WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated todo list in OneDrive");

                // Return the updated list
                return await GetTodoListFromOneDrive(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating todo list in OneDrive");
                throw;
            }
        }

        public async Task<List<TodoItem>> GetTodoListFromOneDrive(string key)
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

                    var records = new List<TodoItem>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var item = new TodoItem
                        {
                            ID = worksheet.Cells[row, 1].Value?.ToString(),
                            Title = worksheet.Cells[row, 2].Value?.ToString(),
                            Description = worksheet.Cells[row, 3].Value?.ToString(),
                            DueDate = DateTime.TryParse(worksheet.Cells[row, 4].Value?.ToString(), out var dueDate) ? dueDate : (DateTime?)null,
                            IsCompleted = bool.Parse(worksheet.Cells[row, 5].Value?.ToString() ?? "false"),
                            Category = worksheet.Cells[row, 6].Value?.ToString() ?? "Uncategorized",
                            ParentTaskId = worksheet.Cells[row, 7].Value?.ToString(),
                            CreatedAt = DateTime.Parse(worksheet.Cells[row, 8].Value?.ToString() ?? DateTime.Now.ToString("MM/dd/yyyy HH:mm")),
                            UpdatedAt = DateTime.Parse(worksheet.Cells[row, 9].Value?.ToString() ?? DateTime.Now.ToString("MM/dd/yyyy HH:mm")),
                            IsDeleted = bool.Parse(worksheet.Cells[row, 10].Value?.ToString() ?? "false"),
                            LastModifiedDate = DateTime.Parse(worksheet.Cells[row, 11].Value?.ToString() ?? DateTime.Now.ToString("MM/dd/yyyy HH:mm")),
                            DeletedDate = DateTime.TryParse(worksheet.Cells[row, 12].Value?.ToString(), out var deletedDate) ? deletedDate : (DateTime?)null
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

        public async Task<List<TodoItem>> AddTodoItem(string key, TodoItem newItem)
        {
            try
            {
                await GetFileContent(key); // Ensure we have the latest content before adding

                var currentItems = await GetTodoListFromOneDrive(key);
                newItem.ID = GenerateNewId(currentItems, string.IsNullOrEmpty(newItem.ParentTaskId));
                // The constructor already sets CreatedAt, UpdatedAt, and LastModifiedDate
                currentItems.Add(newItem);

                var updatedList = await UpdateTodoListInOneDrive(key, currentItems);

                _logger.LogInformation($"Successfully added new item: {newItem.Title}");

                return updatedList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception error adding new todo item");
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

        private string GenerateNewId(List<TodoItem> currentItems, bool isMainTask)
        {
            string prefix = isMainTask ? "MCMT" : "MCST";
            int maxId = currentItems
                .Where(item => item.ID.StartsWith("MC")) // Check for both MCMT and MCST
                .Select(item =>
                {
                    if (int.TryParse(item.ID.Substring(4), out int id))
                        return id;
                    return 0;
                })
                .DefaultIfEmpty(0)
                .Max();
            return $"{prefix}{maxId + 1}";
        }
    }
}