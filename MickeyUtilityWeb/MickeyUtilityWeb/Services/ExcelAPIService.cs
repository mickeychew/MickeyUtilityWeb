using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;

namespace MickeyUtilityWeb.Services
{
    public class ExcelApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly ILogger<ExcelApiService> _logger;
        private const string GRAPH_API_BASE = "https://graph.microsoft.com/v1.0";

        public ExcelApiService(HttpClient httpClient, IAccessTokenProvider tokenProvider, ILogger<ExcelApiService> logger)
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

        public async Task<(int rows, int columns, string address)> GetCurrentRange(string fileId, string worksheetName)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync($"{GRAPH_API_BASE}/me/drive/items/{fileId}/workbook/worksheets/{worksheetName}/usedRange");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadFromJsonAsync<GraphRangeResponse>();
            return (content.Values.Length, content.Values[0].Length, content.Address);
        }

        public async Task UpdateRange(string fileId, string worksheetName, string rangeAddress, List<object[]> updateData)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var updateRange = new { values = updateData };
            var json = JsonSerializer.Serialize(updateRange);

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync($"{GRAPH_API_BASE}/me/drive/items/{fileId}/workbook/worksheets/{worksheetName}/range(address='{rangeAddress}')", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Error updating Excel range: {errorContent}");
            }
        }

        public async Task<byte[]> GetFileContent(string fileId)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var fileContentResponse = await _httpClient.GetAsync($"{GRAPH_API_BASE}/me/drive/items/{fileId}/content");

            if (!fileContentResponse.IsSuccessStatusCode)
            {
                var errorContent = await fileContentResponse.Content.ReadAsStringAsync();
                _logger.LogError($"Error response from API: {fileContentResponse.StatusCode} - {errorContent}");
                throw new HttpRequestException($"Error response from API: {fileContentResponse.StatusCode} - {errorContent}");
            }

            return await fileContentResponse.Content.ReadAsByteArrayAsync();
        }

        public async Task DeleteRow(string fileId, string worksheetName, string rangeAddress)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var deleteResponse = await _httpClient.PostAsync(
                $"{GRAPH_API_BASE}/me/drive/items/{fileId}/workbook/worksheets/{worksheetName}/range(address='{rangeAddress}')/delete",
                new StringContent("{\"shift\": \"Up\"}", System.Text.Encoding.UTF8, "application/json")
            );

            if (!deleteResponse.IsSuccessStatusCode)
            {
                var errorContent = await deleteResponse.Content.ReadAsStringAsync();
                _logger.LogError($"Error response from Excel API when deleting row: {errorContent}");
                throw new HttpRequestException($"Error deleting row from Excel: {errorContent}");
            }
        }

        private class GraphRangeResponse
        {
            public object[][] Values { get; set; }
            public string Address { get; set; }
        }
    }
}