[ApiController]
[Route("api/[controller]")]
public class RangesController : ControllerBase {
    private readonly DatasetService ds;
    public RangesController(DatasetService ds){ this.ds=ds; }

    [HttpPost("validate")]
    public IActionResult Validate([FromBody] RangePayload p) {
        // load processed.csv and count rows within ranges; validate ordering.
        var path = ds.ProcessedPath;
        if(!System.IO.File.Exists(path)) return BadRequest(new { error = "No processed dataset found." });
        // parse timestamps quickly
        var lines = System.IO.File.ReadAllLines(path).Skip(1);
        var timestamps = lines.Select(l => {
            var parts = l.Split(',');
            // assume timestamp is last column
            return DateTime.Parse(parts.Last());
        }).ToList();

        DateTime t1s = DateTime.Parse(p.trainStart);
        DateTime t1e = DateTime.Parse(p.trainEnd);
        DateTime t2s = DateTime.Parse(p.testStart);
        DateTime t2e = DateTime.Parse(p.testEnd);
        DateTime s3s = DateTime.Parse(p.simStart);
        DateTime s3e = DateTime.Parse(p.simEnd);

        // validate ordering
        if(!(t1s<=t1e && t1e < t2s && t2s<=t2e && t2e < s3s && s3s<=s3e)){
            return Ok(new { status="Invalid", message="Ranges must be sequential and non-overlapping."});
        }

        int CountIn(DateTime a, DateTime b) => timestamps.Count(dt => dt >= a && dt <= b);

        var trainCount = CountIn(t1s,t1e);
        var testCount = CountIn(t2s,t2e);
        var simCount = CountIn(s3s,s3e);

        return Ok(new {
            status="Valid",
            counts=new { train=trainCount, test=testCount, sim=simCount },
            durations=new { train=(t1e-t1s).TotalDays, test=(t2e-t2s).TotalDays, sim=(s3e-s3s).TotalDays }
        });
    }
}

public class RangePayload {
    public string trainStart { get; set; }
    public string trainEnd { get; set; }
    public string testStart { get; set; }
    public string testEnd { get; set; }
    public string simStart { get; set; }
    public string simEnd { get; set; }
}
