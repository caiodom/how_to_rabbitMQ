using Minio;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

string endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT");
string accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY");
string secretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY");
bool useSSL = bool.TryParse(Environment.GetEnvironmentVariable("MINIO_USE_SSL"), out useSSL);


// Add Minio using the custom endpoint and configure additional settings for default MinioClient initialization
builder.Services.AddMinio(configureClient => configureClient
    .WithEndpoint(endpoint)
    .WithCredentials(accessKey, secretKey)
    .WithTimeout(TimeSpan.FromMinutes(5).Minutes)
    .WithSSL(false)
    .Build());

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
      app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
                c.RoutePrefix = string.Empty; // Define o Swagger UI como a página inicial da aplicação
            });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
