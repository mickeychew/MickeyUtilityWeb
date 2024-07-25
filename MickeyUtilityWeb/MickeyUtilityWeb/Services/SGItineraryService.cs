using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Text.Json;
using System.Globalization;
using MickeyUtilityWeb.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using OfficeOpenXml;
using static MickeyUtilityWeb.Services.SGItineraryService;
using System.Net.Http;

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

                // Get the current range
                var (currentRows, currentColumns) = await GetCurrentRangeUpdate();

                // Prepare the update data
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

                // Ensure we have at least as many rows as the current range
                while (updateData.Count < currentRows)
                {
                    updateData.Add(new object[currentColumns]);
                }

                // Ensure each row has the correct number of columns
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

                // Prepare the update request
                var updateRange = new { values = updateData };
                var json = JsonSerializer.Serialize(updateRange);
                _logger.LogInformation($"Sending update request with data: {json}");

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Send the update request to update the entire range
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
                _logger.LogInformation($"Starting to add new itinerary item: {JsonSerializer.Serialize(newItem)}");

                var currentItems = await GetItineraryFromOneDrive();
                _logger.LogInformation($"Current number of items: {currentItems.Count}");
                currentItems.Add(newItem);
                _logger.LogInformation($"New number of items after adding: {currentItems.Count}");

                var accessToken = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Get the current range
                var (currentRows, currentColumns, rangeAddress) = await GetCurrentRange();
                _logger.LogInformation($"Current range: Rows={currentRows}, Columns={currentColumns}, Address={rangeAddress}");

                // Prepare the update data
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

                _logger.LogInformation($"Update data rows: {updateData.Count}, columns: {updateData[0].Length}");

                // Calculate the new range address
                string newRangeAddress = $"Sheet1!A1:G{updateData.Count}";
                _logger.LogInformation($"New range address: {newRangeAddress}");

                // Prepare the update request
                var updateRange = new { values = updateData };
                var json = JsonSerializer.Serialize(updateRange);
                _logger.LogInformation($"Sending update request with data: {json}");

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Log the full request details
                _logger.LogInformation($"Request URL: {GRAPH_API_BASE}/me/drive/items/{FILE_ID}/workbook/worksheets/Sheet1/range(address='{newRangeAddress}')");
                _logger.LogInformation($"Request method: PATCH");
                _logger.LogInformation($"Request headers: {string.Join(", ", _httpClient.DefaultRequestHeaders.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                _logger.LogInformation($"Request content: {await content.ReadAsStringAsync()}");

                // Send the update request to update the entire range
                var response = await _httpClient.PatchAsync($"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/workbook/worksheets/Sheet1/range(address='{newRangeAddress}')", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Error response from API: {errorContent}");
                    throw new HttpRequestException($"Error adding new item to OneDrive: {errorContent}");
                }

                var successContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Successfully added new itinerary item. Response: {successContent}");
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

                // First, get the file content
                var fileContentResponse = await _httpClient.GetAsync($"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/content");

                if (!fileContentResponse.IsSuccessStatusCode)
                {
                    var errorContent = await fileContentResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Error response from API: {fileContentResponse.StatusCode} - {errorContent}");
                    throw new HttpRequestException($"Error response from API: {fileContentResponse.StatusCode} - {errorContent}");
                }

                var excelContent = await fileContentResponse.Content.ReadAsByteArrayAsync();

                // Now, process the Excel content
                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0]; // Assuming data is in the first worksheet
                    var rowCount = worksheet.Dimension.Rows;
                    var colCount = worksheet.Dimension.Columns;

                    var records = new List<ItineraryItem>();

                    // Skip the header row
                    for (int row = 2; row <= rowCount; row++)
                    {
                        records.Add(new ItineraryItem
                        {
                            IsChecked = bool.Parse(worksheet.Cells[row, 1].Value?.ToString() ?? "false"),
                            Day = worksheet.Cells[row, 2].Value?.ToString(),
                            Date = DateTime.Parse(worksheet.Cells[row, 3].Value?.ToString() ?? DateTime.MinValue.ToString()),
                            TimeString = worksheet.Cells[row, 4].Value?.ToString(),
                            Activity = worksheet.Cells[row, 5].Value?.ToString(),
                            Icon = worksheet.Cells[row, 6].Value?.ToString(),
                            Location = worksheet.Cells[row, 7].Value?.ToString()
                        });
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

        private TimeEntry ParseTimeEntry(string timeString)
        {
            var times = timeString.Split('-').Select(t => t.Trim()).ToArray();
            if (times.Length == 2)
            {
                return new TimeEntry
                {
                    Start = ParseTime(times[0]),
                    End = ParseTime(times[1])
                };
            }
            else
            {
                return new TimeEntry
                {
                    Start = ParseTime(timeString),
                    End = null
                };
            }
        }

        private TimeSpan? ParseTime(string timeString)
        {
            if (double.TryParse(timeString, NumberStyles.Any, CultureInfo.InvariantCulture, out double excelTime))
            {
                return TimeSpan.FromDays(excelTime);
            }

            string[] formats = { "h:mm", "H:mm", "h:mm tt", "H:mm tt" };
            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(timeString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    return parsedTime.TimeOfDay;
                }
            }

            if (DateTime.TryParse(timeString, out DateTime fallbackParsedTime))
            {
                return fallbackParsedTime.TimeOfDay;
            }

            _logger.LogWarning($"Failed to parse time: {timeString}");
            return null;
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