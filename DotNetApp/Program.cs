using DotNetApp.Controllers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSwaggerGen();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
app.Urls.Add("http://+:8081");

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

await EventHubController.CreateCheckpointBlobForTests();

app.Run();
