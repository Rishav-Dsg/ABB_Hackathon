var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<DatasetService>(); // service to read/write processed.csv
builder.Services.AddHttpClient("ml", client => {
    client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("ML_SERVICE_URL") ?? "http://ml-service:8000");
    client.Timeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddCors(options => options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.MapControllers();
app.Run();
