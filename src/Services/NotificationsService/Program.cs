using System.Data;
using Npgsql;
using NotificationsService.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IDbConnection>(_ =>
    new NpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

var app = builder.Build();

var retries = 10;
while (retries-- > 0)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();
        db.Open();
        var sql = File.ReadAllText("Migrations/001_create_notifications_table.sql");
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB not ready, retrying... ({ex.Message})");
        Thread.Sleep(3000);
    }
}

app.MapControllers();

app.Run();