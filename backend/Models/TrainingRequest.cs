namespace Backend.Models
{
    public class TrainingRequest
    {
        public DateTime TrainStart { get; set; }
        public DateTime TrainEnd { get; set; }
        public DateTime TestStart { get; set; }
        public DateTime TestEnd { get; set; }
        public string DatasetType { get; set; } = "numeric";
    }
}