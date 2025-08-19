[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase {
    private readonly DatasetService ds;
    public UploadController(DatasetService ds) { this.ds = ds; }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file) {
        if (file == null || file.Length == 0) return BadRequest(new { error = "No file" });
        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { error = "Only CSV supported" });
        using var stream = file.OpenReadStream();
        try {
            var meta = await ds.ParseAndSaveAsync(stream, file.FileName);
            return Ok(meta);
        } catch(Exception ex){
            return BadRequest(new { error = ex.Message });
        }
    }
}
