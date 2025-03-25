using Dapper;
using Infrastructure.Data.Sql;
using Contoso.Modules.Loan.Repositories.Queries;
using Contoso.Modules.Loan.Entities;

namespace Contoso.Modules.Loan.Repositories
{
    public class CustomerRepository
    {
        private readonly DapperContext _dapperContext;

        public CustomerRepository(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        public async Task<Customer?> GetCustomerByIdAsync(string customerId)
        {
            using var connection = _dapperContext.CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<Customer>(
                     CustomerQueries.GetCustomerById,
                    new { id = customerId }
            );
            return result;
        }

        public async Task<CustomerCredit?> GetCustomerCreditByIdAsync(string customerId)
        {
            using var connection = _dapperContext.CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<CustomerCredit>(
                     CustomerQueries.GetCustomerCreditById,
                    new { id = customerId }
            );
            return result;
        }
    }
}