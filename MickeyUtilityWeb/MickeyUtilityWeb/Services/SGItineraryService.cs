using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MickeyUtilityWeb.Models;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;

namespace MickeyUtilityWeb.Services
{
    public class SGItineraryService
    {
        private readonly ExcelApiService _excelApiService;
        private readonly ILogger<SGItineraryService> _logger;
        private const string FILE_ID = "85E9FC7E76F38D5C!s12d4646d292c4ec1a42d56ebded4daee";
        private const string WORKSHEET_NAME = "Sheet1";

        public SGItineraryService(ExcelApiService excelApiService, ILogger<SGItineraryService> logger)
        {
            _excelApiService = excelApiService;
            _logger = logger;
        }

        public async Task UpdateItineraryInOneDrive(List<ItineraryItem> itinerary)
        {
            try
            {
                var (currentRows, currentColumns, _) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "IsChecked", "Day", "Date", "Time", "Activity", "Icon", "Location" }
                };

                updateData.AddRange(itinerary.Select(item => new object[]
                {
                    item.IsChecked,
                    item.Day,
                    item.Date.ToString("yyyy-MM-dd"),
                    item.TimeString,
                    item.Activity,
                    item.Icon ?? "",
                    item.Location
                }));

                // Pad the data if necessary
                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[currentColumns]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:G{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated itinerary in OneDrive");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating itinerary in OneDrive");
                throw;
            }
        }

        public async Task AddItineraryItem(ItineraryItem newItem)
        {
            try
            {
                var currentItems = await GetItineraryFromOneDrive();
                currentItems.Add(newItem);

                var (_, _, rangeAddress) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "IsChecked", "Day", "Date", "Time", "Activity", "Icon", "Location" }
                };

                updateData.AddRange(currentItems.Select(item => new object[]
                {
                    item.IsChecked,
                    item.Day,
                    item.Date.ToString("yyyy-MM-dd"),
                    item.TimeString,
                    item.Activity,
                    item.Icon ?? "",
                    item.Location
                }));

                string newRangeAddress = $"{WORKSHEET_NAME}!A1:G{updateData.Count}";

                await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, newRangeAddress, updateData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new itinerary item");
                throw;
            }
        }

        public async Task<List<ItineraryItem>> GetItineraryFromOneDrive()
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

                    var records = new List<ItineraryItem>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var timeString = worksheet.Cells[row, 4].Value?.ToString();

                        var item = new ItineraryItem
                        {
                            IsChecked = bool.Parse(worksheet.Cells[row, 1].Value?.ToString() ?? "false"),
                            Day = worksheet.Cells[row, 2].Value?.ToString(),
                            Date = DateTime.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? DateTime.MinValue.ToString()),
                            TimeString = FormatTimeString(timeString),
                            Activity = worksheet.Cells[row, 5].Value?.ToString(),
                            Icon = worksheet.Cells[row, 6].Value?.ToString(),
                            Location = worksheet.Cells[row, 7].Value?.ToString()
                        };

                        if (!string.IsNullOrWhiteSpace(item.Day) || !string.IsNullOrWhiteSpace(item.Activity))
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

        public async Task DeleteItineraryItem(ItineraryItem itemToDelete)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete itinerary item: {itemToDelete.Activity}");

                var excelContent = await _excelApiService.GetFileContent(FILE_ID);

                int rowToDelete = -1;

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        if (bool.Parse(worksheet.Cells[row, 1].Value?.ToString() ?? "false") == itemToDelete.IsChecked &&
                            worksheet.Cells[row, 2].Value?.ToString() == itemToDelete.Day &&
                            DateTime.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? DateTime.MinValue.ToString()) == itemToDelete.Date &&
                            worksheet.Cells[row, 4].Value?.ToString() == itemToDelete.TimeString &&
                            worksheet.Cells[row, 5].Value?.ToString() == itemToDelete.Activity &&
                            worksheet.Cells[row, 6].Value?.ToString() == itemToDelete.Icon &&
                            worksheet.Cells[row, 7].Value?.ToString() == itemToDelete.Location)
                        {
                            rowToDelete = row;
                            break;
                        }
                    }
                }

                if (rowToDelete == -1)
                {
                    _logger.LogWarning($"Item not found for deletion: {itemToDelete.Activity}");
                    return;
                }

                // Delete the specific row
                var deleteRowRange = $"{WORKSHEET_NAME}!A{rowToDelete}:G{rowToDelete}";
                _logger.LogInformation($"Deleting row range: {deleteRowRange}");

                await _excelApiService.DeleteRow(FILE_ID, WORKSHEET_NAME, deleteRowRange);

                _logger.LogInformation($"Successfully deleted item: {itemToDelete.Activity}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting itinerary item: {itemToDelete.Activity}");
                throw;
            }
        }

        public async Task UploadExcel(byte[] fileContent)
        {
            try
            {
                var newItems = ReadExcelContent(fileContent);
                await ClearExistingContent();
                await AddNewItems(newItems);
                _logger.LogInformation("Successfully uploaded new Excel file content to OneDrive");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading Excel file content to OneDrive");
                throw;
            }
        }

        private List<ItineraryItem> ReadExcelContent(byte[] fileContent)
        {
            var items = new List<ItineraryItem>();
            using (var stream = new MemoryStream(fileContent))
            using (var package = new ExcelPackage(stream))
            {
                var worksheet = package.Workbook.Worksheets[0];
                var rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++) // Assuming first row is header
                {
                    var item = new ItineraryItem
                    {
                        IsChecked = bool.Parse(worksheet.Cells[row, 1].Value?.ToString() ?? "false"),
                        Day = worksheet.Cells[row, 2].Value?.ToString(),
                        Date = DateTime.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? DateTime.MinValue.ToString()),
                        TimeString = worksheet.Cells[row, 4].Value?.ToString(),
                        Activity = worksheet.Cells[row, 5].Value?.ToString(),
                        Icon = worksheet.Cells[row, 6].Value?.ToString(),
                        Location = worksheet.Cells[row, 7].Value?.ToString()
                    };
                    items.Add(item);
                }
            }
            return items;
        }
        private async Task ClearExistingContent()
        {
            var (rowCount, _, _) = await _excelApiService.GetCurrentRange(FILE_ID, WORKSHEET_NAME);
            if (rowCount > 1) // Keep the header row
            {
                var clearRange = $"{WORKSHEET_NAME}!A2:G{rowCount}";
                var clearData = Enumerable.Repeat(new object[7], rowCount - 1).ToList();
                await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, clearRange, clearData);
            }
        }

        private async Task AddNewItems(List<ItineraryItem> items)
        {
            var updateData = items.Select(item => new object[]
            {
                item.IsChecked,
                item.Day,
                item.Date.ToString("yyyy-MM-dd"),
                item.TimeString,
                item.Activity,
                item.Icon ?? "",
                item.Location
            }).ToList();

            var rangeAddress = $"{WORKSHEET_NAME}!A2:G{updateData.Count + 1}";
            await _excelApiService.UpdateRange(FILE_ID, WORKSHEET_NAME, rangeAddress, updateData);
        }
    

    private string FormatTimeString(string timeString)
        {
            if (string.IsNullOrWhiteSpace(timeString))
            {
                return string.Empty;
            }

            if (DateTime.TryParse(timeString, out DateTime parsedDateTime))
            {
                return parsedDateTime.ToString("HH:mm");
            }

            var times = timeString.Split('-').Select(t => t.Trim()).ToArray();
            if (times.Length == 2)
            {
                var formattedStart = FormatSingleTime(times[0]);
                var formattedEnd = FormatSingleTime(times[1]);
                return $"{formattedStart} - {formattedEnd}";
            }
            else
            {
                var formattedTime = FormatSingleTime(timeString);
                return formattedTime;
            }
        }

        private string FormatSingleTime(string timeString)
        {
            if (DateTime.TryParse(timeString, out DateTime parsedDateTime))
            {
                return parsedDateTime.ToString("HH:mm");
            }

            if (DateTime.TryParseExact(timeString, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
            {
                return parsedTime.ToString("HH:mm");
            }
            else if (DateTime.TryParseExact(timeString, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedTime))
            {
                return parsedTime.ToString("HH:mm");
            }
            else if (double.TryParse(timeString, out double excelTime))
            {
                var convertedTime = DateTime.FromOADate(excelTime).ToString("HH:mm");
                return convertedTime;
            }

            return timeString; // Return original string if parsing fails
        }
    }
}