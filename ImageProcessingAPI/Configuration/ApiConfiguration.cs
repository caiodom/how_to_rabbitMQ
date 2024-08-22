using CoreAdapters.Extensions;
using Core.Contracts;

namespace ImageProcessingAPI.Configuration
{
    public static class ApiConfiguration
    {
        private const string CORS_NAME = "Total";
        public static void AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Add services to the container.

            services.AddControllers();

            services.AddCors(options =>
            {
                options.AddPolicy(CORS_NAME,
                    builder =>
                        builder
                            .AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader());
            });


            services.AddMinioConfigurations();
            services.Configure<RabbitMQSettings>(configuration.GetSection("RabbitMQ"));
            services.AddRabbitMQConfigurations();


            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
        }


        public static void UseApiConfiguration(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
                    c.RoutePrefix = string.Empty; // Define o Swagger UI como a página inicial da aplicação
                });
            }

            app.UseHttpsRedirection();

            app.UseCors(CORS_NAME);
            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
     
        }

    }
}
