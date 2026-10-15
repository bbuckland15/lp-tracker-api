using LpTracker.Api.Data;
using LpTracker.Api.Riot;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database: SQLite file locally. Swapping to SQL Server or Postgres (e.g. on AWS RDS)
// is a one-line change here plus a different EF Core provider package.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LpTracker")));

// Riot API: settings from the "Riot" config section (API key from user-secrets),
// a typed HttpClient, the sync logic, and the background worker that runs it on a timer.
builder.Services.Configure<RiotOptions>(builder.Configuration.GetSection(RiotOptions.SectionName));
builder.Services.AddHttpClient<IRiotApiClient, RiotApiClient>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<RiotSyncService>();
builder.Services.AddHostedService<RiotSyncWorker>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Create the database on first run and add sample data.
// Next step for a real deployment: switch to EF Core migrations (dotnet ef migrations add ...).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (app.Environment.IsDevelopment())
        DbSeeder.SeedIfEmpty(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(); // interactive docs at /swagger
}

app.MapControllers();

app.Run();

// Lets test projects reference this assembly's entry point if integration tests are added later.
public partial class Program { }
