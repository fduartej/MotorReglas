using Dapper;
using Infrastructure.Data.Sql;
using Contoso.Modules.Insight.Repositories.Queries;
using Contoso.Modules.Insight.Entities;

namespace Contoso.Modules.Insight.Repositories
{
    public class CustomerHistoryPreCalculatedRepository
    {
        private readonly DapperContext _dapperContext;

        public CustomerHistoryPreCalculatedRepository(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        public async Task<IEnumerable<CustomerHistoryPreCalculated>> GetCustomerHistoryPreCalculatedAsync(string customerId, string period)
        {
            using var connection = _dapperContext.CreateConnection();
            var results = await connection.QueryAsync<CustomerHistoryPreCalculated>(
                     CustomerHistoryPreCalculatedQueries.LisCustomerPreCalculedVariable,
                    new { customerId = customerId, period = period }
            );
            return results.ToList();
        }

    }
}