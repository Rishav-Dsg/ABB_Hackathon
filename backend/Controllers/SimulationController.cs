[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase {
    private readonly IHttpClientFactory _clientFactory;
    private readonly DatasetService ds;
    public SimulationController(IHttpClientFactory clientFactory, DatasetService ds){
        _clientFactory = clientFactory;
        this.ds = ds;
    }

    [HttpPost("start")]
    public async Task StartSimulation([FromBody] SimPayload payload) {
        Response.ContentType = "text/event-stream";
        Response.StatusCode = 200;
        var client = _clientFactory.CreateClient("ml");
        var url = $"/simulate?simStart={Uri.EscapeDataString(payload.simStart)}&simEnd={Uri.EscapeDataString(payload.simEnd)}";
        using var mlResponse = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        using var mlStream = await mlResponse.Content.ReadAsStreamAsync();
        var buffer = new byte[8192];
        int read;
        while((read = await mlStream.ReadAsync(buffer, 0, buffer.Length)) > 0){
            await Response.Body.WriteAsync(buffer, 0, read);
            await Response.Body.FlushAsync();
        }
    }
}

public class SimPayload {
    public string simStart { get; set; }
    public string simEnd { get; set; }
}
