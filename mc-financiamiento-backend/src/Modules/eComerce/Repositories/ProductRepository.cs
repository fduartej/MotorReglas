using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contoso.Modules.eComerce.Entities;
using Infrastructure.Data.NoSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Contoso.Modules.eComerce.Repositories
{
    public class ProductRepository
    {
        private readonly CosmosContext _context;
        private readonly ILogger<ProductRepository> _logger;

        public ProductRepository(CosmosContext context,
            ILogger<ProductRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Product> GetProductByIdAsync(Guid id)
        {
            try
            {
                return await _context.DbSetProducts.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting product by id");
                return null;
            }
        }

        public async Task<IEnumerable<Product>> GetProductsAsync()
        {
            try
            {
                var products = await _context.DbSetProducts.ToListAsync();
                _logger.LogInformation("Retrieved {Count} products", products.Count);
                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting products");
                return null;
            }
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
        {
            try
            {
                return await _context.DbSetProducts
                    .Where(p => p.Category == category)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting products by category");
                return null;
            }
        }


    }
}