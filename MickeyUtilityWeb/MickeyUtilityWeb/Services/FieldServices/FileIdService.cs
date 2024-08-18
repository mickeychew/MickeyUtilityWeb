using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;

namespace MickeyUtilityWeb.Services
{
    public class FileIdService
    {
        private readonly ExcelApiService _excelApiService;
        private readonly ILogger<FileIdService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _masterFileId;
        private const string WORKSHEET_NAME = "Sheet1";
        private Dictionary<string, string> _fileIdCache;

        public FileIdService(ExcelApiService excelApiService, ILogger<FileIdService> logger, IConfiguration configuration)
        {
            _excelApiService = excelApiService;
            _logger = logger;
            _configuration = configuration;
            _masterFileId = _configuration["MasterFileId"];
            _fileIdCache = new Dictionary<string, string>();
        }

        public async Task<string> GetFileId(string key)
        {
            if (_fileIdCache.TryGetValue(key, out string fileId))
            {
                return fileId;
            }
            await RefreshFileIdCache();
            if (_fileIdCache.TryGetValue(key, out fileId))
            {
                return fileId;
            }
            throw new KeyNotFoundException($"File ID for key '{key}' not found.");
        }

        private async Task RefreshFileIdCache()
        {
            try
            {
                if (string.IsNullOrEmpty(_masterFileId))
                {
                    throw new InvalidOperationException("MasterFileId is not set in the configuration.");
                }

                var excelContent = await _excelApiService.GetFileContent(_masterFileId);
                using (var stream = new MemoryStream(excelContent))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[WORKSHEET_NAME];
                    var rowCount = worksheet.Dimension.Rows;
                    _fileIdCache.Clear();
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var key = worksheet.Cells[row, 1].Value?.ToString();
                        var value = worksheet.Cells[row, 2].Value?.ToString();
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            _fileIdCache[key] = value;
                        }
                    }
                }
                _logger.LogInformation("File ID cache refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing File ID cache");
                throw;
            }
        }
    }
}