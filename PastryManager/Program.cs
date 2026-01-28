using PastryManager.Application;
using PastryManager.Infrastructure;
using PastryManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PastryManager API", Version = "v1" });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Auto-migrate database in development with retry logic
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        var retryCount = 0;
        var maxRetries = 10;
        var delay = TimeSpan.FromSeconds(2);
        
        while (retryCount < maxRetries)
        {
            try
            {
                logger.LogInformation("Attempting to migrate database... (Attempt {Attempt}/{MaxRetries})", retryCount + 1, maxRetries);
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database migration completed successfully.");
                break;
            }
            catch (Exception ex) when (retryCount < maxRetries - 1)
            {
                retryCount++;
                logger.LogWarning(ex, "Database migration failed. Retrying in {Delay} seconds...", delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }
    }
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
