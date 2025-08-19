[ApiController]
[Route("api/[controller]")]
public class ModelController : ControllerBase {
    private readonly IHttpClientFactory _clientFactory;
    public ModelController(IHttpClientFactory clientFactory){ _clientFactory = clientFactory; }

    [HttpPost("train")]
    public async Task<IActionResult> Train([FromBody] TrainPayload payload){
        var client = _clientFactory.CreateClient("ml");
        var res = await client.PostAsJsonAsync("/train", payload);
        var content = await res.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }
}
public class TrainPayload {
    public string trainStart { get; set; }
    public string trainEnd { get; set; }
    public string testStart { get; set; }
    public string testEnd { get; set; }
}
