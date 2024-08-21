﻿using System;
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

        private async Task<string> GetFileId()
        {
            return await _fileIdService.GetFileId("TodoList");
        }

        public async Task<List<TodoItem>> UpdateTodoListInOneDrive(List<TodoItem> todoList)
        {
            try
            {
                string fileId = await GetFileId();
                await GetFileContent(); // Ensure we have the latest content before updating

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
                    item.DueDate?.ToString("MM/dd/yyyy HH:mm"),
                    item.IsCompleted,
                    item.Category,
                    item.ParentTaskId,
                    item.CreatedAt.ToString("MM/dd/yyyy HH:mm"),
                    item.UpdatedAt.ToString("MM/dd/yyyy HH:mm"),
                    item.IsDeleted,
                    item.LastModifiedDate.ToString("MM/dd/yyyy HH:mm"),
                    item.DeletedDate?.ToString("MM/dd/yyyy HH:mm")
                }));

                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[12]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:L{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(fileId, WORKSHEET_NAME, rangeAddress, updateData);

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
                string fileId = await GetFileId();
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

        public async Task<List<TodoItem>> AddTodoItem(TodoItem newItem)
        {
            try
            {
                await GetFileContent(); // Ensure we have the latest content before adding

                var currentItems = await GetTodoListFromOneDrive();
                newItem.ID = GenerateNewId(currentItems, string.IsNullOrEmpty(newItem.ParentTaskId));
                newItem.CreatedAt = DateTime.Now;
                newItem.UpdatedAt = DateTime.Now;
                newItem.LastModifiedDate = DateTime.Now;
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
                var itemToRemove = currentItems.FirstOrDefault(i => i.ID == itemToDelete.ID);

                if (itemToRemove != null)
                {
                    itemToRemove.IsDeleted = true;
                    itemToRemove.DeletedDate = DateTime.Now;
                    itemToRemove.LastModifiedDate = DateTime.Now;

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
                string fileId = await GetFileId();
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