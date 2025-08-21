namespace Backend.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }

    public class UploadResponse
    {
        public string FileName { get; set; }
        public int TotalRecords { get; set; }
        public int TotalColumns { get; set; }
        public double PassRate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class ValidationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public int TrainingRecords { get; set; }
        public int TestingRecords { get; set; }
        public int SimulationRecords { get; set; }
    }

    public class TrainingResponse
    {
        public double Accuracy { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }
        public double AucScore { get; set; }
        public Dictionary<string, int> ConfusionMatrix { get; set; }
        public Dictionary<string, double> DatasetInfo { get; set; }
    }

    public class SimulationResponse
    {
        public List<SimulationRecord> Records { get; set; }
        public SimulationSummary Summary { get; set; }
    }

    public class SimulationRecord
    {
        public string Timestamp { get; set; }
        public string Id { get; set; }
        public string Prediction { get; set; }
        public double Confidence { get; set; }
        public double Temperature { get; set; }
        public double Pressure { get; set; }
        public double Humidity { get; set; }
    }

    public class SimulationSummary
    {
        public int TotalPredictions { get; set; }
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        public double AverageConfidence { get; set; }
    }
}