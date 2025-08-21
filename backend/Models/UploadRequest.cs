namespace Backend.Models
{
    public class UploadRequest
    {
        public IFormFile File { get; set; }
        public string DatasetType { get; set; } = "numeric";
    }
}