using Application.Services;
using Domain.Contracts;
using Domain.Entities;
using Infrastructure.Contexts;
using Infrastructure.Contracts;
using Infrastructure.MessageQueue;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();
builder.Services.AddScoped<IRepository<Product>, ProductsRepository>();
builder.Services.AddScoped<CreateProductService>();

builder.Services.AddSingleton<IMessageBus>(sp =>
    new RabbitMQMessageBus(builder.Configuration.GetValue<string>("RabbitMQ:ConnectionString"))
);

builder.Services.AddDbContext<ProductContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using var scope = app.Services.CreateScope();

var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
var productService = scope.ServiceProvider.GetRequiredService<CreateProductService>();
var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

bus.Consume<Product>("create_product_queue", async product =>
{
    await productService.CreateProduct(product);
});

bus.Consume<IEnumerable<Product>>("decrease_stock_queue", async products =>
{
    using var innerScope = scopeFactory.CreateScope();
    var productsRepository = innerScope.ServiceProvider.GetRequiredService<IRepository<Product>>();
    
    foreach (var p in products)
    {
        var dbProduct = await productsRepository.Read(p.Id);
        if (dbProduct != null)
        {
            dbProduct.Amount -= p.Amount;
            await productsRepository.Update(dbProduct);
        }
    }
});

app.UseHttpsRedirection();
app.MapControllers();

app.Run();