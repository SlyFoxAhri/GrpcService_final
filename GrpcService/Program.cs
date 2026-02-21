using GrpcService.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();


builder.Services.AddEndpointsApiExplorer();

builder.Services.AddGrpc();
builder.Services.AddMySqlDataSource(builder.Configuration.GetConnectionString("Default"));


var app = builder.Build();


app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<JunkyardService>();

app.Run();