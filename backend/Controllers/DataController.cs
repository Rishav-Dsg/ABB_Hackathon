using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataController : ControllerBase
    {
        private readonly IDataService _dataService;
        private readonly ILogger<DataController> _logger;

        public DataController(IDataService dataService, ILogger<DataController> logger)
        {
            _dataService = dataService;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<ApiResponse<UploadResponse>> Upload([FromForm] UploadRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                {
                    return new ApiResponse<UploadResponse> { Success = false, Message = "No file uploaded." };
                }

                if (!request.File.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    return new ApiResponse<UploadResponse> { Success = false, Message = "File must be CSV format." };
                }

                var result = await _dataService.ProcessDataset(request.File, request.DatasetType);
                return new ApiResponse<UploadResponse> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during upload.");
                return new ApiResponse<UploadResponse> { Success = false, Message = ex.Message };
            }
        }

        [HttpPost("validate-dates")]
        public async Task<ApiResponse<ValidationResponse>> ValidateDates([FromBody] DateRangeRequest request)
        {
            try
            {
                var result = await _dataService.ValidateDateRanges(request);
                return new ApiResponse<ValidationResponse> { Success = result.IsValid, Message = result.Message, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating dates.");
                return new ApiResponse<ValidationResponse> { Success = false, Message = ex.Message };
            }
        }

        [HttpPost("train")]
        public async Task<ApiResponse<TrainingResponse>> Train([FromBody] TrainingRequest request)
        {
            try
            {
                var result = await _dataService.TrainModel(request);
                return new ApiResponse<TrainingResponse> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during training.");
                return new ApiResponse<TrainingResponse> { Success = false, Message = ex.Message };
            }
        }

        [HttpPost("simulate")]
        public async Task<ApiResponse<SimulationResponse>> Simulate([FromBody] SimulationRequest request)
        {
            try
            {
                var result = await _dataService.StartSimulation(request);
                return new ApiResponse<SimulationResponse> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during simulation.");
                return new ApiResponse<SimulationResponse> { Success = false, Message = ex.Message };
            }
        }
    }
}