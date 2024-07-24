using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Text.Json;
using System.Globalization;
using MickeyUtilityWeb.Models;
using Microsoft.Extensions.Logging;

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
                var tokenResult = await _tokenProvider.RequestAccessToken(
                    new AccessTokenRequestOptions
                    {
                        Scopes = new[] { "https://graph.microsoft.com/Files.Read.All" }
                    });

                if (tokenResult.TryGetToken(out var token))
                {
                    _logger.LogInformation("Access token acquired successfully");
                    return token.Value;
                }

                if (tokenResult.Status == AccessTokenResultStatus.RequiresRedirect)
                {
                    _logger.LogWarning("Authentication redirect required");
                    throw new Exception("Authentication required. Please log in.");
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

        public async Task<List<ItineraryItem>> GetItineraryFromOneDrive()
        {
            try
            {
                var accessToken = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var contentResponse = await _httpClient.GetAsync($"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/workbook/worksheets/Sheet1/usedRange");

                if (contentResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized access. Attempting to refresh token.");
                    accessToken = await GetAccessTokenAsync();
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    contentResponse = await _httpClient.GetAsync($"{GRAPH_API_BASE}/me/drive/items/{FILE_ID}/workbook/worksheets/Sheet1/usedRange");
                }

                contentResponse.EnsureSuccessStatusCode();
                var rangeContent = await contentResponse.Content.ReadFromJsonAsync<GraphRangeResponse>();

                if (rangeContent?.Values == null || rangeContent.Values.Length <= 1)
                {
                    _logger.LogWarning("No data found in the worksheet.");
                    return new List<ItineraryItem>();
                }

                var records = new List<ItineraryItem>();

                // Skip the header row
                for (int i = 1; i < rangeContent.Values.Length; i++)
                {
                    var row = rangeContent.Values[i];
                    if (row.Length >= 7)
                    {
                        try
                        {
                            records.Add(new ItineraryItem
                            {
                                IsChecked = ParseBoolean(row[0]),
                                Day = row[1].ToString(),
                                Date = ParseDateTime(row[2]),
                                Time = ParseTimeEntry(row[3].ToString()),
                                Activity = row[4].ToString(),
                                Icon = row[5].ToString(),
                                Location = row[6].ToString()
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error parsing row {i}: {string.Join(", ", row.Select(r => r.ToString()))}");
                        }
                    }
                }

                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from OneDrive");
                throw;
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
            //_logger.LogInformation($"Parsing time entry: {timeString}");
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
            //_logger.LogInformation($"Parsing individual time: {timeString}");

            // Try parsing as a decimal (Excel time)
            if (double.TryParse(timeString, NumberStyles.Any, CultureInfo.InvariantCulture, out double excelTime))
            {
                //_logger.LogInformation($"Parsed as Excel time: {excelTime}");
                return TimeSpan.FromDays(excelTime);
            }

            // Try parsing with various formats
            string[] formats = { "h:mm", "H:mm", "h:mm tt", "H:mm tt" };
            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(timeString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    //_logger.LogInformation($"Successfully parsed {timeString} using format {format}");
                    return parsedTime.TimeOfDay;
                }
            }

            // If all else fails, try a more lenient parse
            if (DateTime.TryParse(timeString, out DateTime fallbackParsedTime))
            {
                //_logger.LogInformation($"Fallback parsing succeeded for {timeString}");
                return fallbackParsedTime.TimeOfDay;
            }

            _logger.LogWarning($"Failed to parse time: {timeString}");
            return null;
        }

        public class GraphRangeResponse
        {
            public JsonElement[][] Values { get; set; }
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