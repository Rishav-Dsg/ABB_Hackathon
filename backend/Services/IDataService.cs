using Backend.Models;

namespace Backend.Services
{
    public interface IDataService
    {
        Task<UploadResponse> ProcessDataset(IFormFile file, string datasetType);
        Task<ValidationResponse> ValidateDateRanges(DateRangeRequest request);
        Task<TrainingResponse> TrainModel(TrainingRequest request);
        Task<SimulationResponse> StartSimulation(SimulationRequest request);
        Task<int> GetRecordCount(DateTime start, DateTime end, string datasetType);
    }
}