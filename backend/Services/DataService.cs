using Backend.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Backend.Services
{
    public class DataService : IDataService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DataService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _mlServiceBaseUrl;

        public DataService(IWebHostEnvironment environment, ILogger<DataService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _environment = environment;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _mlServiceBaseUrl = configuration["MLService:BaseUrl"] ?? "http://ml-service:8000";
        }

        public async Task<UploadResponse> ProcessDataset(IFormFile file, string datasetType)
        {
            try
            {
                var uploadsFolder = Path.Combine(_environment.ContentRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var filePath = Path.Combine(uploadsFolder, file.FileName);
                
                // Save original file
                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Augment with synthetic timestamps if needed (must happen before copying to shared volume)
                await AugmentTimestampsIfNeeded(filePath);

                // Save augmented file to shared volume for ML service
                // Try saving to shared volume path used in containers.
                // On local Windows dev environments, writing to root (e.g., C:\app\data) can be blocked.
                // Fall back to a project-local data directory if the primary path fails.
                try
                {
                    var mlSharedPath = Path.Combine("/app/data", "upload.csv");
                    Directory.CreateDirectory(Path.GetDirectoryName(mlSharedPath)!);
                    File.Copy(filePath, mlSharedPath, true);
                }
                catch
                {
                    var localDataDir = Path.Combine(_environment.ContentRootPath, "data");
                    if (!Directory.Exists(localDataDir))
                    {
                        Directory.CreateDirectory(localDataDir);
                    }
                    var fallbackPath = Path.Combine(localDataDir, "upload.csv");
                    File.Copy(filePath, fallbackPath, true);
                }

                // Analyze augmented file
                var (totalRecords, totalColumns, passRate) = await AnalyzeCsv(filePath);
                
                // Generate start/end based on 1-sec per row
                var startDate = new DateTime(2021, 1, 1, 0, 0, 0);
                var endDate = startDate.AddSeconds(totalRecords - 1);

                return new UploadResponse
                {
                    FileName = file.FileName,
                    TotalRecords = totalRecords,
                    TotalColumns = totalColumns,
                    PassRate = passRate,
                    StartDate = startDate,
                    EndDate = endDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing dataset");
                throw;
            }
        }

        public async Task<ValidationResponse> ValidateDateRanges(DateRangeRequest request)
        {
            try
            {
                // Validate date logic
                if (request.TrainingStart > request.TrainingEnd ||
                    request.TestingStart > request.TestingEnd ||
                    request.SimulationStart > request.SimulationEnd)
                {
                    return new ValidationResponse
                    {
                        IsValid = false,
                        Message = "Each period must have a start date before end date"
                    };
                }

                if (request.TrainingEnd > request.TestingStart)
                {
                    return new ValidationResponse
                    {
                        IsValid = false,
                        Message = "Training period must end before testing period starts"
                    };
                }

                if (request.TestingEnd > request.SimulationStart)
                {
                    return new ValidationResponse
                    {
                        IsValid = false,
                        Message = "Testing period must end before simulation period starts"
                    };
                }

                // Ensure selected dates are within the dataset's available window if dataset exists
                var datasetStart = new DateTime(2021, 1, 1, 0, 0, 0);
                var totalRows = TryGetDatasetTotalRows();
                if (totalRows > 0)
                {
                    var datasetEnd = datasetStart.AddSeconds(totalRows - 1);
                    bool outOfBounds =
                        request.TrainingStart < datasetStart || request.TrainingEnd > datasetEnd ||
                        request.TestingStart < datasetStart || request.TestingEnd > datasetEnd ||
                        request.SimulationStart < datasetStart || request.SimulationEnd > datasetEnd;
                    if (outOfBounds)
                    {
                        return new ValidationResponse
                        {
                            IsValid = false,
                            Message = $"Selected dates must be within dataset window {datasetStart:yyyy-MM-dd} to {datasetEnd:yyyy-MM-dd}"
                        };
                    }
                }

                // Get record counts for each period
                var trainingRecords = await GetRecordCount(request.TrainingStart, request.TrainingEnd, request.DatasetType);
                var testingRecords = await GetRecordCount(request.TestingStart, request.TestingEnd, request.DatasetType);
                var simulationRecords = await GetRecordCount(request.SimulationStart, request.SimulationEnd, request.DatasetType);

                if (trainingRecords == 0 || testingRecords == 0 || simulationRecords == 0)
                {
                    return new ValidationResponse
                    {
                        IsValid = false,
                        Message = "One or more periods contain no data"
                    };
                }

                return new ValidationResponse
                {
                    IsValid = true,
                    Message = "Date ranges validated successfully",
                    TrainingRecords = trainingRecords,
                    TestingRecords = testingRecords,
                    SimulationRecords = simulationRecords
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating date ranges");
                return new ValidationResponse
                {
                    IsValid = false,
                    Message = $"Validation error: {ex.Message}"
                };
            }
        }

        public async Task<TrainingResponse> TrainModel(TrainingRequest request)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var mlServiceUrl = $"{_mlServiceBaseUrl}/train-model";

                var trainingRequest = new
                {
                    trainStart = request.TrainStart,
                    trainEnd = request.TrainEnd,
                    testStart = request.TestStart,
                    testEnd = request.TestEnd,
                    data_path = GetDatasetPath(request.DatasetType)
                };

                var json = JsonSerializer.Serialize(trainingRequest);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(mlServiceUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new Exception($"ML service error ({(int)response.StatusCode}): {errorBody}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<TrainingResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? throw new Exception("Failed to deserialize training response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error training model");
                throw;
            }
        }

        public async Task<SimulationResponse> StartSimulation(SimulationRequest request)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var mlServiceUrl = $"{_mlServiceBaseUrl}/simulate-json";

                var simulationRequest = new
                {
                    simulationStart = request.SimulationStart,
                    simulationEnd = request.SimulationEnd,
                    data_path = GetDatasetPath(request.DatasetType)
                };

                var json = JsonSerializer.Serialize(simulationRequest);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(mlServiceUrl, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SimulationResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? throw new Exception("Failed to deserialize simulation response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting simulation");
                throw;
            }
        }

        public async Task<int> GetRecordCount(DateTime start, DateTime end, string datasetType)
        {
            // Calculate record count based on synthetic timestamps
            var startDate = new DateTime(2021, 1, 1, 0, 0, 0);
            var startIndex = (int)(start - startDate).TotalSeconds;
            var endIndex = (int)(end - startDate).TotalSeconds;

            return Math.Max(0, endIndex - startIndex + 1);
        }

        private async Task AugmentTimestampsIfNeeded(string filePath)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ","
            };

            // Check if timestamp column exists
            bool hasTimestamp;
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, config))
            {
                await csv.ReadAsync();
                csv.ReadHeader();
                hasTimestamp = csv.HeaderRecord.Contains("synthetic_timestamp");
            }

            if (hasTimestamp) return; // No need to augment

            // Augment: Read and write with added column
            var tempFilePath = filePath + ".temp";
            var startTime = new DateTime(2021, 1, 1, 0, 0, 0);
            int rowIndex = 0;

            using (var reader = new StreamReader(filePath))
            using (var csvReader = new CsvReader(reader, config))
            await using (var writer = new StreamWriter(tempFilePath))
            await using (var csvWriter = new CsvWriter(writer, config))
            {
                await csvReader.ReadAsync();
                csvReader.ReadHeader();

                // Write new header with added column
                foreach (var header in csvReader.HeaderRecord)
                {
                    csvWriter.WriteField(header);
                }
                csvWriter.WriteField("synthetic_timestamp");
                await csvWriter.NextRecordAsync();

                // Write rows with timestamps
                while (await csvReader.ReadAsync())
                {
                    for (int i = 0; i < csvReader.HeaderRecord.Length; i++)
                    {
                        csvWriter.WriteField(csvReader.GetField(i));
                    }
                    csvWriter.WriteField(startTime.AddSeconds(rowIndex).ToString("yyyy-MM-dd HH:mm:ss"));
                    await csvWriter.NextRecordAsync();
                    rowIndex++;
                }
            }

            // Replace original with augmented
            File.Delete(filePath);
            File.Move(tempFilePath, filePath);
        }

        private async Task<(int totalRecords, int totalColumns, double passRate)> AnalyzeCsv(string filePath)
        {
            int sampleSize = 0;
            int passCount = 0;  // Spec: pass rate = % rows with Response == 1
            var columns = new List<string>();

            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                await csv.ReadAsync();
                csv.ReadHeader();
                columns = csv.HeaderRecord.ToList();

                bool hasResponse = columns.Contains("Response");

                // Sample first 1000 rows for passRate estimate
                while (await csv.ReadAsync() && sampleSize < 1000)
                {
                    if (hasResponse)
                    {
                        var response = csv.GetField<int>("Response");
                        if (response == 1) passCount++;  // 1 = pass per spec
                    }
                    sampleSize++;
                }
            }

            // Get total row count
            var totalRows = await CountTotalRows(filePath);

            // Estimate passRate from sample (avoid division by zero)
            var passRate = sampleSize > 0 ? (double)passCount / sampleSize * 100 : 0;

            return (totalRows, columns.Count, passRate);
        }

        private async Task<int> CountTotalRows(string filePath)
        {
            int count = 0;
            using (var reader = new StreamReader(filePath))
            {
                while (await reader.ReadLineAsync() != null)
                {
                    count++;
                }
            }
            return count - 1; // Subtract header
        }

        private string GetDatasetPath(string datasetType)
        {
            // Use the shared volume path so both backend and ML service see the same file
            return Path.Combine("/app/data", "upload.csv");
        }

        private int TryGetDatasetTotalRows()
        {
            try
            {
                var pathsToCheck = new List<string>
                {
                    Path.Combine("/app/data", "upload.csv"),
                    Path.Combine(_environment.ContentRootPath, "data", "upload.csv"),
                    Path.Combine(_environment.ContentRootPath, "uploads", "upload.csv")
                };
                foreach (var p in pathsToCheck)
                {
                    if (File.Exists(p))
                    {
                        return CountTotalRows(p).GetAwaiter().GetResult();
                    }
                }
            }
            catch { }
            return 0;
        }
    }
}