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
    public class ItineraryService
    {
        private readonly ExcelApiService _excelApiService;
        private readonly FileIdService _fileIdService;
        private readonly ILogger<ItineraryService> _logger;
        private const string WORKSHEET_NAME = "Sheet1";

        public ItineraryService(ExcelApiService excelApiService, FileIdService fileIdService, ILogger<ItineraryService> logger)
        {
            _excelApiService = excelApiService;
            _fileIdService = fileIdService;
            _logger = logger;
        }

        private static readonly Dictionary<string, string> Icons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"Home", "🏠 Home"},
        {"Plane", "✈️ Flight"},
        {"Utensils", "🍴 Meal"},
        {"Hotel", "🏨 Hotel"},
        {"Coffee", "☕ Cafe"},
        {"Camera", "📷 Sightseeing"},
        {"Sun", "☀️ Outdoor Activity"},
        {"Car", "🚗 Drive"},
        {"Train", "🚂 Train"},
        {"Bus", "🚌 Bus"},
        {"Ship", "🚢 Cruise"},
        {"Bicycle", "🚲 Cycling"},
        {"Walking", "🚶 Walking Tour"},
        {"Shopping", "🛒 Shopping"},
        {"Museum", "🏛️ Museum"},
        {"Monument", "🗽 Monument"},
        {"Beach", "🏖️ Beach"},
        {"Mountain", "⛰️ Mountain"},
        {"Park", "🏞️ Park"},
        {"Restaurant", "🍽️ Restaurant"},
        {"Bar", "🍸 Bar"},
        {"Theater", "🎭 Theater"},
        {"Movie", "🎬 Movie"},
        {"Music", "🎵 Concert"},
        {"Swimming", "🏊 Swimming"},
        {"Gym", "🏋️ Gym"},
        {"Spa", "💆 Spa"},
        {"Library", "📚 Library"},
        {"University", "🎓 University"},
        {"Hospital", "🏥 Hospital"}
    };

        public static string GetIconDescription(string iconKey)
        {
            return Icons.TryGetValue(iconKey ?? "", out var description) ? description : "📌 Other";
        }

        public static Dictionary<string, string> GetIcons()
        {
            return Icons;
        }

        public async Task<List<ExcelListItem>> GetAvailableLists()
        {
            return await _fileIdService.GetFileIdsByService("ItineraryList");
        }

        private async Task<string> GetFileId(string key)
        {
            return await _fileIdService.GetFileId(key);
        }

        public async Task UpdateItineraryInOneDrive(string key, List<ItineraryItem> itinerary)
        {
            try
            {
                string fileId = await GetFileId(key);
                var (currentRows, currentColumns, _) = await _excelApiService.GetCurrentRange(fileId, WORKSHEET_NAME);

                var updateData = new List<object[]>
                {
                    new object[] { "ID", "IsChecked", "Day", "Date", "StartTime", "EndTime", "Activity", "Icon", "Location", "CreatedAt", "UpdatedAt", "IsDeleted", "LastModifiedDate", "DeletedDate" }
                };

                foreach (var item in itinerary)
                {
                    try
                    {
               
                        updateData.Add(new object[]
                        {
                            item.ID,
                            item.IsChecked,
                            item.Day,
                            item.Date.ToString("yyyy-MM-dd"),
                            FormatTimeSpan(item.StartTime),
                            FormatTimeSpan(item.EndTime),
                             item.Activity,
                            item.Icon,
                        
        
                            item.Location,
                            item.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                            item.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                            item.IsDeleted,
                            item.LastModifiedDate.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                            item.DeletedDate?.ToString("yyyy-MM-ddTHH:mm:ss.fff") ?? ""
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error formatting item for update: {item.ID}");
                        // Skip this item and continue with the next
                    }
                }

                // Pad the data if necessary
                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[currentColumns]);
                }

                string rangeAddress = $"{WORKSHEET_NAME}!A1:N{Math.Max(currentRows, updateData.Count)}";

                await _excelApiService.UpdateRange(fileId, WORKSHEET_NAME, rangeAddress, updateData);

                _logger.LogInformation("Successfully updated itinerary in OneDrive");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating itinerary in OneDrive");
                throw;
            }
        }

        public async Task AddItineraryItem(string key, ItineraryItem newItem)
        {
            try
            {
                string fileId = await GetFileId(key);
                var currentItems = await GetItineraryFromOneDrive(key);
                newItem.ID = GenerateNewId(currentItems);
                newItem.CreatedAt = DateTime.UtcNow;
                newItem.UpdatedAt = DateTime.UtcNow;
                newItem.LastModifiedDate = DateTime.UtcNow;
                newItem.IsDeleted = false;
                currentItems.Add(newItem);

                await UpdateItineraryInOneDrive(key, currentItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new itinerary item");
                throw;
            }
        }

        public async Task<List<ItineraryItem>> GetItineraryFromOneDrive(string key)
        {
            try
            {
                string fileId = await GetFileId(key);
                var excelContent = await _excelApiService.GetFileContent(fileId);

                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;
                    var colCount = worksheet.Dimension.Columns;

                    var records = new List<ItineraryItem>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                       

                        var item = new ItineraryItem
                        {
                            ID = worksheet.Cells[row, 1].Value?.ToString(),
                            IsChecked = bool.Parse(worksheet.Cells[row, 2].Value?.ToString() ?? "false"),
                            Day = int.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? "0"),
                            Date = ParseExcelDate(worksheet.Cells[row, 4].Value),
                            StartTime = ParseTimeSpan(worksheet.Cells[row, 5].Value?.ToString()),
                            EndTime = ParseTimeSpan(worksheet.Cells[row, 6].Value?.ToString()),
                            Activity = worksheet.Cells[row, 7].Value?.ToString(),
                            Icon = Icons.ContainsKey(worksheet.Cells[row, 8].Value?.ToString() ?? "")
                ? worksheet.Cells[row, 8].Value?.ToString()
                : "Other",
                            Location = worksheet.Cells[row, 9].Value?.ToString(),
                            CreatedAt = ParseExcelDate(worksheet.Cells[row, 10].Value),
                            UpdatedAt = ParseExcelDate(worksheet.Cells[row, 11].Value),
                            IsDeleted = bool.Parse(worksheet.Cells[row, 12].Value?.ToString() ?? "false"),
                            LastModifiedDate = ParseExcelDate(worksheet.Cells[row, 13].Value),
                            DeletedDate = ParseNullableExcelDate(worksheet.Cells[row, 14].Value)
                        };

                        if (!string.IsNullOrWhiteSpace(item.ID))
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


        public async Task DeleteItineraryItem(string key, ItineraryItem itemToDelete)
        {
            try
            {
                var currentItems = await GetItineraryFromOneDrive(key);
                var itemToRemove = currentItems.FirstOrDefault(i => i.ID == itemToDelete.ID);
                if (itemToRemove != null)
                {
                    itemToRemove.IsDeleted = true;
                    itemToRemove.DeletedDate = DateTime.UtcNow;
                    itemToRemove.LastModifiedDate = DateTime.UtcNow;
                    await UpdateItineraryInOneDrive(key, currentItems);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting itinerary item: {itemToDelete.Activity}");
                throw;
            }
        }

        public async Task UploadExcel(string key, byte[] fileContent)
        {
            try
            {
                _logger.LogInformation("Starting Excel upload process");
                var newItems = ReadExcelContent(fileContent);
                _logger.LogInformation($"Read {newItems.Count} items from uploaded Excel file");
                await UpdateItineraryInOneDrive(key, newItems);
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


                        var item = new ItineraryItem
                        {
                            ID = worksheet.Cells[row, 1].Value?.ToString() ?? GenerateNewId(items),
                            IsChecked = bool.Parse(worksheet.Cells[row, 2].Value?.ToString() ?? "false"),
                            Day = int.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? "0"),
                            Date = ParseExcelDate(worksheet.Cells[row, 4].Value),
                            StartTime = ParseTimeSpan(worksheet.Cells[row, 5].Value?.ToString()),
                            EndTime = ParseTimeSpan(worksheet.Cells[row, 6].Value?.ToString()),
                            Activity = worksheet.Cells[row, 7].Value?.ToString(),
                            Icon = worksheet.Cells[row, 8].Value?.ToString() ?? "Other",
                            Location = worksheet.Cells[row, 9].Value?.ToString(),
                            CreatedAt = ParseExcelDate(worksheet.Cells[row, 10].Value),
                            UpdatedAt = ParseExcelDate(worksheet.Cells[row, 11].Value),
                            IsDeleted = bool.Parse(worksheet.Cells[row, 12].Value?.ToString() ?? "false"),
                            LastModifiedDate = ParseExcelDate(worksheet.Cells[row, 13].Value),
                            DeletedDate = ParseNullableDateTime(worksheet.Cells[row, 14].Value?.ToString())
                        };

                        _logger.LogInformation($"Parsed item: Day={item.Day}, Date={item.Date}, StartTime={item.StartTime}, EndTime={item.EndTime}, Icon={item.Icon}");
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

        private string GenerateNewId(List<ItineraryItem> currentItems)
        {
            string prefix = "MCIT";
            int maxId = currentItems
                .Where(item => item.ID.StartsWith(prefix))
                .Select(item =>
                {
                    if (int.TryParse(item.ID.Substring(prefix.Length), out int id))
                        return id;
                    return 0;
                })
                .DefaultIfEmpty(0)
                .Max();
            return $"{prefix}{maxId + 1}";
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

        private DateTime? ParseNullableExcelDate(object cellValue)
        {
            if (cellValue == null)
                return null;

            if (cellValue is DateTime dateTime)
                return dateTime;

            if (double.TryParse(cellValue.ToString(), out double excelDate))
                return DateTime.FromOADate(excelDate);

            if (DateTime.TryParse(cellValue.ToString(), out DateTime parsedDate))
                return parsedDate;

            _logger.LogWarning($"Unable to parse nullable date: {cellValue}");
            return null;
        }

        private TimeSpan ParseTimeSpan(string timeString)
        {
            if (string.IsNullOrWhiteSpace(timeString))
                return TimeSpan.Zero;

            if (TimeSpan.TryParse(timeString, out TimeSpan result))
                return result;

            if (double.TryParse(timeString, out double excelTime))
            {
                // Excel time is a fraction of a day
                return TimeSpan.FromDays(excelTime);
            }

            if (DateTime.TryParse(timeString, out DateTime dateTime))
                return dateTime.TimeOfDay;

            _logger.LogWarning($"Unable to parse time: {timeString}");
            return TimeSpan.Zero;
        }

        private string FormatTimeSpan(TimeSpan time)
        {
            try
            {
                // Handle cases where TimeSpan might be more than 24 hours
                int totalHours = (int)time.TotalHours;
                int minutes = time.Minutes;

                // Use 24-hour format to avoid AM/PM issues
                return $"{totalHours:D2}:{minutes:D2}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error formatting TimeSpan: {time}");
                return "00:00"; // Return a default value in case of error
            }
        }

        private DateTime? ParseNullableDateTime(string dateTimeString)
        {
            if (string.IsNullOrWhiteSpace(dateTimeString))
                return null;

            if (DateTime.TryParse(dateTimeString, out DateTime result))
                return result;

            return null;
        }
    }
}