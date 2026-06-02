using SpeechToText.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Load local settings for development secrets (ignored by Git)
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllers();

// Configure CORS to allow the frontend to access the API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Custom Services as Singletons
builder.Services.AddSingleton<IAudioConverter, FFmpegAudioConverter>();
builder.Services.AddSingleton<ITranscriptionService, WhisperTranscriptionService>();
builder.Services.AddSingleton<IDocumentExporter, WordDocumentExporter>();
builder.Services.AddSingleton<IYouTubeService, YouTubeService>();
builder.Services.AddSingleton<IBilibiliService, BilibiliService>();
builder.Services.AddSingleton<ITranslationService, TranslationService>();
builder.Services.AddSingleton<IVideoBurnService, VideoBurnService>();
builder.Services.AddSingleton<IYouTubeDownloadTracker, YouTubeDownloadTracker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true) // Enable Swagger in production/dev for ease of testing
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowVueApp");

app.UseAuthorization();

app.MapControllers();

// Ensure folders are created and FFmpeg is ready asynchronously when app starts
var audioConverter = app.Services.GetRequiredService<IAudioConverter>();
var transcriptionService = app.Services.GetRequiredService<ITranscriptionService>();

// Warm up resources in a background task
_ = Task.Run(async () =>
{
    try
    {
        Console.WriteLine("Pre-initializing services on startup...");
        await audioConverter.EnsureFFmpegAsync();
        await transcriptionService.EnsureModelDownloadedAsync("base"); // Warm up default base model
        Console.WriteLine("Startup services initialization complete.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Pre-initialization failed (this will be retried on demand): {ex.Message}");
    }
});

app.Run();
