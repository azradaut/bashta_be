using Bashta.Core.Interfaces;
using Bashta.Core.Services;
using Bashta.Infrastructure.Data;
using Bashta.Infrastructure.External;
using Bashta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Bashta.ML.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<BashtaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<IPlantPotRepository, PlantPotRepository>();
builder.Services.AddScoped<IPlantRepository, PlantRepository>();
builder.Services.AddScoped<ISensorReadingRepository, SensorReadingRepository>();
builder.Services.AddScoped<IWateringEventRepository, WateringEventRepository>();
builder.Services.AddScoped<IDiseaseDetectionRepository, DiseaseDetectionRepository>();
builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IPlantTypeRepository, PlantTypeRepository>();
builder.Services.AddScoped<IDiseaseRepository, DiseaseRepository>();

// Services
builder.Services.AddScoped<RecommendationService>();
//builder.Services.AddScoped<WateringRuleEngine>();
builder.Services.AddScoped<DLIService>();
builder.Services.AddSingleton<ITomatoDiseasePredictionService, OnnxTomatoDiseasePredictionService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// External
builder.Services.AddHttpClient<WeatherService>();
builder.Services.AddScoped<WateringRuleEngine>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// ML
var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "bashta_tomato_model.onnx");
var classNamesPath = Path.Combine(AppContext.BaseDirectory, "Models", "class_names.txt");
builder.Services.AddSingleton(new DiseaseClassifier(modelPath, classNamesPath));

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.Run();