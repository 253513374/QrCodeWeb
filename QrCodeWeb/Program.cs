using QrCodeWeb.Services;
using Serilog;
using Microsoft.AspNetCore.Hosting.Server;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

Log.Information("Serilog 日志记录启动成功");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // builder.WebHost.UseKestrel();
    builder.Host.UseSerilog((x, y) => y.WriteTo.Console().ReadFrom.Configuration(x.Configuration));
    // Add services to the container.

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddScoped<DeCodeService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "serilog 初始化异常：");
    throw;
}
finally
{
    Log.Information("serilog 关闭完成");
    Log.CloseAndFlush();
}