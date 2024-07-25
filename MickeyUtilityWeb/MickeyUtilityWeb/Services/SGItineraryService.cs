using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Text.Json;
using System.Globalization;
using MickeyUtilityWeb.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using OfficeOpenXml;

namespace MickeyUtilityWeb.Services
{
    public class SGItineraryService
    {
        private readonly HttpClient _httpClient;
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly ILogger<SGItineraryService> _logger;
        private const string FILE_NAME = "SGItinerary.xlsx";
        private const string FILE_ID = "85E9FC7E76F38D5C!s12d4646d292c4ec1a42d56ebded4daee";
        private const string GRAPH_API_BASE = "https://graph.microsoft.com/v1.0";

        public SGItineraryService(HttpClient httpClient, IAccessTokenProvider tokenProvider, ILogger<SGItineraryService> logger)
        {
            _httpClient = httpClient;
            _tokenProvider = tokenProvider;
            _logger = logger;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            try
            {
                var scopes = new[] { "Files.ReadWrite" };
                var tokenResult = await _tokenProvider.RequestAccessToken(
                    new AccessTokenRequestOptions
                    {
                        Scopes = scopes
                    });

                if (tokenResult.TryGetToken(out var token))
                {
                    _logger.LogInformation("Access token acquired successfully");
                    return token.Value;
                }

                if (tokenResult.Status == AccessTokenResultStatus.RequiresRedirect)
                {
                    _logger.LogWarning("Authentication redirect required");
                    var redirectUrl = tokenResult.RedirectUrl;
                    throw new Exception($"Authentication required. Please navigate to: {redirectUrl}");
                }

                _logger.LogWarning("Failed to acquire access token");
                throw new InvalidOperationException("Couldn't acquire an access token");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring access token");
                throw;
            }
        }

        public async Task UpdateItineraryInOneDrive(List<ItineraryItem> itinerary)
        {
            try
            {
                var accessToken = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var (currentRows, currentColumns) = await GetCurrentRangeUpdate();

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

                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[currentColumns]);
                }

                for (int i = 0; i < updateData.Count; i++)
                {
                    if (updateData[i].Length < currentColumns)
                    {
                        var paddedRow = new List<object>(updateData[i]);
                        while (paddedRow.Count < currentColumns)
                        {
                            paddedRow.Add(null);
                        }
                        updateData[i] = paddedRow.ToArray();
                    }
                }

                var updateRange = new { values = updateData };
                var json = JsonSerializer.Serialize(updateRange);
                _logger.LogInformation($"Sending update request with data: {json}");

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync($"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/workbook/worksheets/Sheet1/usedRange", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Error response from API: {errorContent}");
                    throw new HttpRequestException($"Error updating OneDrive: {errorContent}");
                }

                _logger.LogInformation("Successfully updated itinerary in OneDrive");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating itinerary in OneDrive");
                throw;
            }
        }

        private async Task<(int rows, int columns)> GetCurrentRangeUpdate()
        {
            var response = await _httpClient.GetAsync($"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/workbook/worksheets/Sheet1/usedRange");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadFromJsonAsync<GraphRangeResponse>();
            return (content.Values.Length, content.Values[0].Length);
        }

        public class GraphRangeResponse
        {
            public object[][] Values { get; set; }
            public string Address { get; set; }
        }

        private async Task<(int rows, int columns, string address)> GetCurrentRange()
        {
            var response = await _httpClient.GetAsync($"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/workbook/worksheets/Sheet1/usedRange");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadFromJsonAsync<GraphRangeResponse>();
            return (content.Values.Length, content.Values[0].Length, content.Address);
        }

        public async Task AddItineraryItem(ItineraryItem newItem)
        {
            try
            {
                var currentItems = await GetItineraryFromOneDrive();
                currentItems.Add(newItem);

                var accessToken = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var (currentRows, currentColumns, rangeAddress) = await GetCurrentRange();

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

                string newRangeAddress = $"Sheet1!A1:G{updateData.Count}";

                var updateRange = new { values = updateData };
                var json = JsonSerializer.Serialize(updateRange);

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync($"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/workbook/worksheets/Sheet1/range(address='{newRangeAddress}')", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error adding new item to OneDrive: {errorContent}");
                }
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
                var accessToken = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var fileContentResponse = await _httpClient.GetAsync($"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/content");

                if (!fileContentResponse.IsSuccessStatusCode)
                {
                    var errorContent = await fileContentResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Error response from API: {fileContentResponse.StatusCode} - {errorContent}");
                    throw new HttpRequestException($"Error response from API: {fileContentResponse.StatusCode} - {errorContent}");
                }

                var excelContent = await fileContentResponse.Content.ReadAsByteArrayAsync();

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
        public async Task DeleteItineraryItem(int rowIndex)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete itinerary item at row index {rowIndex}");

                var accessToken = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var (currentRows, currentColumns, rangeAddress) = await GetCurrentRange();
                _logger.LogInformation($"Current range: Rows={currentRows}, Columns={currentColumns}, Address={rangeAddress}");

                if (rowIndex < 1 || rowIndex >= currentRows)
                {
                    _logger.LogError($"Invalid row index: {rowIndex}. Valid range is 1 to {currentRows - 1}");
                    throw new ArgumentOutOfRangeException(nameof(rowIndex), "Invalid row index");
                }

                // Delete the specific row
                var deleteRowRange = $"Sheet1!A{rowIndex}:G{rowIndex}";
                _logger.LogInformation($"Deleting row range: {deleteRowRange}");
                var deleteResponse = await _httpClient.PostAsync(
                    $"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/workbook/worksheets/Sheet1/range(address='{deleteRowRange}')/delete",
                    new StringContent("{\"shift\": \"Up\"}", System.Text.Encoding.UTF8, "application/json")
                );

                if (!deleteResponse.IsSuccessStatusCode)
                {
                    var errorContent = await deleteResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Error response from OneDrive when deleting row: {errorContent}");
                    throw new HttpRequestException($"Error deleting row from OneDrive: {errorContent}");
                }

                _logger.LogInformation($"Successfully deleted item at row index {rowIndex}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting itinerary item at row index {rowIndex}");
                throw;
            }
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

        private class DateTimeConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return DateTime.Parse(reader.GetString());
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
            }
        }

        private bool ParseBoolean(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
            {
                return element.GetBoolean();
            }
            if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString();
                if (bool.TryParse(stringValue, out bool result))
                {
                    return result;
                }
                return stringValue?.ToLower() is "yes" or "true" or "1";
            }
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt32() != 0;
            }
            return false;
        }

        private DateTime ParseDateTime(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString();
                if (DateTime.TryParse(stringValue, out DateTime result))
                {
                    return result;
                }
            }
            else if (element.ValueKind == JsonValueKind.Number)
            {
                var daysSince1900 = element.GetDouble();
                return DateTime.FromOADate(daysSince1900);
            }
            return DateTime.MinValue;
        }
    }

    public class TimeEntry
    {
        public TimeSpan? Start { get; set; }
        public TimeSpan? End { get; set; }

        public override string ToString()
        {
            if (End.HasValue)
            {
                return $"{FormatTime(Start)} - {FormatTime(End)}";
            }
            else
            {
                return FormatTime(Start);
            }
        }

        private string FormatTime(TimeSpan? time)
        {
            return time?.ToString("hh\\:mm") ?? "";
        }
    }
}