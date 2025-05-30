using MotivationQuotesAPI.Models;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;

Env.Load();
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<QuotesDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(9, 3, 0)))
);

builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

app.Urls.Add("http://*:8080");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    db.Database.Migrate(); 
}

app.Run();
