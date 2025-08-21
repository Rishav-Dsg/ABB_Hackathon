namespace Backend.Models
{
    public class DateRangeRequest
    {
        public DateTime TrainingStart { get; set; }
        public DateTime TrainingEnd { get; set; }
        public DateTime TestingStart { get; set; }
        public DateTime TestingEnd { get; set; }
        public DateTime SimulationStart { get; set; }
        public DateTime SimulationEnd { get; set; }
        public string DatasetType { get; set; } = "numeric";
    }
}