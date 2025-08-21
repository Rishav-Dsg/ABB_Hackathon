namespace Backend.Models
{
    public class SimulationRequest
    {
        public DateTime SimulationStart { get; set; }
        public DateTime SimulationEnd { get; set; }
        public string DatasetType { get; set; } = "numeric";
    }
}