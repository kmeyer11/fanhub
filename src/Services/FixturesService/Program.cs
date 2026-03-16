using System.Data;
using Npgsql;
using FixturesService.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IDbConnection>(_ =>
    new NpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IFixtureRepository, FixtureRepository>();

var app = builder.Build();

app.MapControllers();

app.Run();