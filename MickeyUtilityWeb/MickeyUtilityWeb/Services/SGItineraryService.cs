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
                _logger.LogInformation("Starting Excel upload process");
                var newItems = ReadExcelContent(fileContent);
                _logger.LogInformation($"Read {newItems.Count} items from uploaded Excel file");
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
                    try
                    {
                        var timeString = worksheet.Cells[row, 4].Value?.ToString();
                        _logger.LogInformation($"Reading row {row}, Time value: '{timeString}'");

                        var item = new ItineraryItem
                        {
                            IsChecked = bool.Parse(worksheet.Cells[row, 1].Value?.ToString() ?? "false"),
                            Day = worksheet.Cells[row, 2].Value?.ToString(),
                            Date = ParseExcelDate(worksheet.Cells[row, 3].Value),
                            TimeString = ParseExcelTime(timeString),
                            Activity = worksheet.Cells[row, 5].Value?.ToString(),
                            Icon = worksheet.Cells[row, 6].Value?.ToString(),
                            Location = worksheet.Cells[row, 7].Value?.ToString()
                        };

                        _logger.LogInformation($"Parsed item: Day={item.Day}, Date={item.Date}, Time={item.TimeString}");
                        items.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error parsing row {row}");
                    }
                }
            }
            return items;
        }

        private DateTime ParseExcelDate(object cellValue)
        {
            if (cellValue == null)
                return DateTime.MinValue;

            if (cellValue is DateTime dateTime)
                return dateTime;

            if (double.TryParse(cellValue.ToString(), out double excelDate))
                return DateTime.FromOADate(excelDate);

            if (DateTime.TryParse(cellValue.ToString(), out DateTime parsedDate))
                return parsedDate;

            _logger.LogWarning($"Unable to parse date: {cellValue}");
            return DateTime.MinValue;
        }

        private string ParseExcelTime(string timeString)
        {
            if (string.IsNullOrWhiteSpace(timeString))
                return string.Empty;

            _logger.LogInformation($"Parsing time: '{timeString}'");

            // Try parsing as a time range (e.g., "11:45 - 13:00")
            var timeParts = timeString.Split('-').Select(t => t.Trim()).ToArray();
            if (timeParts.Length == 2)
            {
                var start = FormatSingleTime(timeParts[0]);
                var end = FormatSingleTime(timeParts[1]);
                var result = $"{start} - {end}";
                _logger.LogInformation($"Parsed time range: '{result}'");
                return result;
            }

            // Try parsing as a single time
            var formattedTime = FormatSingleTime(timeString);
            _logger.LogInformation($"Parsed single time: '{formattedTime}'");
            return formattedTime;
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
            // Try parsing as DateTime
            if (DateTime.TryParse(timeString, out DateTime dateTime))
                return dateTime.ToString("HH:mm");

            // Try parsing specific formats
            string[] formats = { "H:mm", "HH:mm", "h:mm tt", "hh:mm tt" };
            if (DateTime.TryParseExact(timeString, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                return parsedTime.ToString("HH:mm");

            // Try parsing as Excel time (decimal fraction of a day)
            if (double.TryParse(timeString, out double excelTime))
                return DateTime.FromOADate(excelTime).ToString("HH:mm");

            _logger.LogWarning($"Unable to parse time: {timeString}");
            return timeString; // Return original if parsing fails
        }
    }
}