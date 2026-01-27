using GrpcService.Services;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddMySqlDataSource(builder.Configuration.GetConnectionString("Default"));

var app = builder.Build();

app.MapGrpcService<JunkyardService>();

app.Run();