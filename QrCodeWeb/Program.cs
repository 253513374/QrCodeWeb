using QrCodeWeb.Services;
using Serilog;
using Microsoft.AspNetCore.Hosting.Server;
using System.Net;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Serilog ��־��¼�����ɹ�");

try
{
    var builder = WebApplication.CreateBuilder(args);

    //builder.WebHost.UseKestrel(options =>
    //{
    //    options.Listen(IPAddress.Any, 5060);
    //});

    // builder.WebHost.UseKestrel();
    builder.Host.UseSerilog((x, y) => y.WriteTo.Console().ReadFrom.Configuration(x.Configuration));
    // Add services to the container.

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddScoped<DeCodeService>();
    builder.Services.AddScoped<CutImageService>();

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
    Log.Fatal(exception, "serilog ��ʼ���쳣��");
    throw;
}
finally
{
    Log.Information("serilog �ر����");
    Log.CloseAndFlush();
}