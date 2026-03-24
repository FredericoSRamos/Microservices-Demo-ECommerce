using Application.DTOs;
using Application.Factories;
using Domain.Contracts;
using Domain.Entities;
using Infrastructure.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class SalesController : ControllerBase
{
    private readonly IRepository<Sale> _salesRepository;
    private readonly SaleFactory _saleFactory;
    private readonly HttpClient _stockHttpClient;
    private readonly IMessageBus _bus;
    
    public SalesController(IRepository<Sale> salesRepository, SaleFactory saleFactory, IHttpClientFactory httpClientFactory, IMessageBus bus)
    {
        _salesRepository = salesRepository;
        _saleFactory = saleFactory;
        _stockHttpClient = httpClientFactory.CreateClient("StockAPI");
        _bus = bus;
    }
    
    #region CRUD
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var sales = await _salesRepository.GetAll();
        
        return Ok(new { Data = sales });
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var sale = await _salesRepository.Read(id);

        if (sale == null)
        {
            return NotFound("Sale not found");
        }
        
        return Ok(new { Data = sale });
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] SaleCreateDTO saleData)
    {
        var sale = _saleFactory.CreateSale(saleData);
        
        try
        {
            await _salesRepository.Create(sale);
            return Ok(sale);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] Sale saleData)
    {
        try
        {
            var sale = await _salesRepository.Update(saleData);

            return Ok(new { Data = sale });
        }
        catch (ArgumentNullException)
        {
            return NotFound("Sale does not exist");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _salesRepository.Delete(id);
        }
        catch (ArgumentNullException)
        {
            return NotFound("Sale does not exist");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
        
        return NoContent();
    }
    #endregion

    [HttpPost("Purchase")]
    public async Task<IActionResult> Purchase([FromBody] ProductListDTO productList)
    {
        var price = 0M;
        foreach (var productData in productList.Products)
        {
            var response = await _stockHttpClient.GetAsync(_stockHttpClient.BaseAddress + productData.Id.ToString());

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, "An error occurred!");
            }

            var product = await response.Content.ReadFromJsonAsync<ProductDTO>();

            if (product.Amount < productData.Amount)
            {
                return UnprocessableEntity(new
                {
                    error = "Insufficient stock",
                    available = product.Amount,
                    requested = productData.Amount
                });
            }

            price += product.Price;
        }

        var saleData = new SaleCreateDTO
        {
            CustomerId = productList.CustomerId,
            Price = price,
            SaleProducts = productList.Products
        };
        
        var result = await Post(saleData);

        if (result is OkObjectResult)
        {
            await _bus.PublishAsync("decrease_stock_queue", productList.Products);
        }

        return result;
    }
}