using ImageProcessingAPI.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiConfiguration(builder.Configuration);


var app = builder.Build();

app.UseApiConfiguration(builder.Environment);

app.Run();
