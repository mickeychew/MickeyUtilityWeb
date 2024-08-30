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
        private Dictionary<string, ExcelListItem> _fileIdCache;

        public FileIdService(ExcelApiService excelApiService, ILogger<FileIdService> logger, IConfiguration configuration)
        {
            _excelApiService = excelApiService;
            _logger = logger;
            _configuration = configuration;
            _masterFileId = configuration["MasterFileId"];
            _fileIdCache = new Dictionary<string, ExcelListItem>();
        }

        public async Task<string> GetFileId(string key)
        {
            if (_fileIdCache.TryGetValue(key, out ExcelListItem item))
            {
                return item.FILE_ID;
            }
            await RefreshFileIdCache();
            if (_fileIdCache.TryGetValue(key, out item))
            {
                return item.FILE_ID;
            }
            throw new KeyNotFoundException($"File ID for key '{key}' not found.");
        }

        public async Task<List<ExcelListItem>> GetFileIdsByService(string service)
        {
            await RefreshFileIdCache();
            return _fileIdCache.Values.Where(item => item.Services == service).ToList();
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
                        var item = new ExcelListItem
                        {
                            ID = worksheet.Cells[row, 1].Value?.ToString(),
                            Key = worksheet.Cells[row, 2].Value?.ToString(),
                            FILE_ID = worksheet.Cells[row, 3].Value?.ToString(),
                            Services = worksheet.Cells[row, 4].Value?.ToString(),
                            DatabaseTableName = worksheet.Cells[row, 5].Value?.ToString(),
                            IsTableCreated = bool.Parse(worksheet.Cells[row, 6].Value?.ToString() ?? "false"),
                            CreatedAt = DateTime.Parse(worksheet.Cells[row, 7].Value?.ToString() ?? DateTime.Now.ToString()),
                            UpdatedAt = DateTime.Parse(worksheet.Cells[row, 8].Value?.ToString() ?? DateTime.Now.ToString()),
                            IsDeleted = bool.Parse(worksheet.Cells[row, 9].Value?.ToString() ?? "false"),
                            LastModifiedDate = DateTime.Parse(worksheet.Cells[row, 10].Value?.ToString() ?? DateTime.Now.ToString()),
                            DeletedDate = DateTime.TryParse(worksheet.Cells[row, 11].Value?.ToString(), out var deletedDate) ? deletedDate : (DateTime?)null
                        };
                        if (!string.IsNullOrEmpty(item.Key))
                        {
                            _fileIdCache[item.Key] = item;
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

    public class ExcelListItem
    {
        public string ID { get; set; }
        public string Key { get; set; }
        public string FILE_ID { get; set; }
        public string Services { get; set; }
        public string DatabaseTableName { get; set; }
        public bool IsTableCreated { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
    }
}