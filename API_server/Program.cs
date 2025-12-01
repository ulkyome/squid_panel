using SquidManagerAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Squid Manager API - Debian",
        Version = "v1",
        Description = "API для управления Squid и SquidGuard на Debian",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Support",
            Email = "support@example.com"
        }
    });

    // Включение XML комментариев для Swagger
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Register services
builder.Services.AddScoped<ISquidService, SquidService>();
builder.Services.AddScoped<ISquidGuardService, SquidGuardService>();

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

// Логирование
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
});

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI(c =>
{
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Squid Manager API v1");
        c.RoutePrefix = "swagger";
});


app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Явное указание URL для прослушивания
app.Urls.Add("http://0.0.0.0:5000");
app.Urls.Add("http://0.0.0.0:8080");

app.Run();