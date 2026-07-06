using System.Diagnostics;
using MarketplacesGoodsTracker.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace MarketplacesGoodsTracker.Services;

public class WbParserService
{
    private readonly string _workerPath;
    private readonly ILogger<WbParserService> _logger;

    public WbParserService(ILogger<WbParserService> logger)
    {
        _logger = logger;
        // Path relative to where the bot is running.
        // Assuming we run from MarketplacesGoodsTracker or root.
        _workerPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../wb-private-api/worker.js"));

        // Fallback for production/different run modes
        if (!File.Exists(_workerPath))
        {
             _workerPath = "wb-private-api/worker.js";
        }
    }

    public async Task<WbProductResponse?> GetProductAsync(string sku)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{_workerPath}\" {sku}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Worker exited with code {Code}. Error: {Error}", process.ExitCode, error);
                return null;
            }

            return JsonConvert.DeserializeObject<WbProductResponse>(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run WB worker for SKU {SKU}", sku);
            return null;
        }
    }
}
