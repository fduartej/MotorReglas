using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Contoso.Modules.eComerce.Entities;
using Contoso.Modules.eComerce.Repositories;
using Microsoft.AspNetCore.Http;

namespace Contoso.Modules.eComerce.Controllers
{
    [ApiController]
    [Route("ecomerce")]
    public class CatalogoController : ControllerBase
    {
        private readonly ILogger<CatalogoController> _logger;
        private readonly ProductRepository _productRepository;

        public CatalogoController(ILogger<CatalogoController> logger,
            ProductRepository productRepository)
        {
            _logger = logger;
            _productRepository = productRepository;
        }

        [HttpGet("catalogo")]
        [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCatalogo()
        {
            _logger.LogInformation("Starting GetCatalogo action.");
            try
            {
                var products = await _productRepository.GetProductsAsync();
                _logger.LogInformation("Successfully retrieved products. Count: {Count}", products.Count());
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving the catalog.");
                return BadRequest("An error occurred while retrieving the catalog.");
            }
        }

        [HttpGet("catalogo/{id}")]
        [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProductById([FromRoute] Guid id)
        {
            _logger.LogInformation("Starting GetProductById action with ID: {Id}", id);
            try
            {
                var product = await _productRepository.GetProductByIdAsync(id);
                if (product == null)
                {
                    _logger.LogWarning("Product with ID: {Id} not found.", id);
                    return NotFound($"Product with ID: {id} not found.");
                }
                _logger.LogInformation("Successfully retrieved product with ID: {Id}", id);
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving the product with ID: {Id}", id);
                return BadRequest("An error occurred while retrieving the product.");
            }
        }

        [HttpPost("catalogo/category/{category}")]
        [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProductByCategory([FromRoute] string category)
        {
            _logger.LogInformation("Starting GetProductByCategory action with Category: {Category}", category);
            try
            {
                var products = await _productRepository.GetProductsByCategoryAsync(category);
                if (!products.Any())
                {
                    _logger.LogWarning("No products found for category: {Category}", category);
                    return NotFound($"No products found for category: {category}");
                }
                _logger.LogInformation("Successfully retrieved products for category: {Category}. Count: {Count}", category, products.Count());
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving products for category: {Category}", category);
                return BadRequest("An error occurred while retrieving products.");
            }
        }
    }
}