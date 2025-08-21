using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly IDataService _dataService;
        private readonly ILogger<ApiController> _logger;

        public ApiController(IDataService dataService, ILogger<ApiController> logger)
        {
            _dataService = dataService;
            _logger = logger;
        }

        [HttpPost("upload-dataset")]
        public async Task<ActionResult<ApiResponse<UploadResponse>>> UploadDataset([FromForm] UploadRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest(new ApiResponse<UploadResponse>
                    {
                        Success = false,
                        Message = "No file uploaded"
                    });
                }

                if (Path.GetExtension(request.File.FileName).ToLower() != ".csv")
                {
                    return BadRequest(new ApiResponse<UploadResponse>
                    {
                        Success = false,
                        Message = "Only CSV files are allowed"
                    });
                }

                var result = await _dataService.ProcessDataset(request.File, request.DatasetType);

                return Ok(new ApiResponse<UploadResponse>
                {
                    Success = true,
                    Message = "Dataset uploaded successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading dataset");
                return StatusCode(500, new ApiResponse<UploadResponse>
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpPost("validate-date-ranges")]
        public async Task<ActionResult<ApiResponse<ValidationResponse>>> ValidateDateRanges([FromBody] DateRangeRequest request)
        {
            try
            {
                var result = await _dataService.ValidateDateRanges(request);

                return Ok(new ApiResponse<ValidationResponse>
                {
                    Success = result.IsValid,
                    Message = result.Message,
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating date ranges");
                return StatusCode(500, new ApiResponse<ValidationResponse>
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpPost("train-model")]
        public async Task<ActionResult<ApiResponse<TrainingResponse>>> TrainModel([FromBody] TrainingRequest request)
        {
            try
            {
                var result = await _dataService.TrainModel(request);

                return Ok(new ApiResponse<TrainingResponse>
                {
                    Success = true,
                    Message = "Model trained successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error training model");
                return StatusCode(500, new ApiResponse<TrainingResponse>
                {
                    Success = false,
                    Message = $"Training failed: {ex.Message}"
                });
            }
        }

        [HttpPost("start-simulation")]
        public async Task<ActionResult<ApiResponse<SimulationResponse>>> StartSimulation([FromBody] SimulationRequest request)
        {
            try
            {
                var result = await _dataService.StartSimulation(request);

                return Ok(new ApiResponse<SimulationResponse>
                {
                    Success = true,
                    Message = "Simulation started successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting simulation");
                return StatusCode(500, new ApiResponse<SimulationResponse>
                {
                    Success = false,
                    Message = $"Simulation failed: {ex.Message}"
                });
            }
        }
    }
}