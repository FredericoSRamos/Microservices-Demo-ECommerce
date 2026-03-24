using Domain.Contracts;
using Infrastructure.Context;
using Infrastructure.MessageQueue;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

builder.Services.AddSingleton<IMessageBus>(sp =>
    new RabbitMQMessageBus(builder.Configuration.GetValue<string>("RabbitMQ:ConnectionString"))
);


builder.Services.AddDbContext<SalesContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection"));
});

builder.Services.AddHttpClient("StockAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("Microservices:Stock"));
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();