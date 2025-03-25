using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Contoso.Modules.Loan.Repositories.Queries
{
    public static class CustomerQueries
    {
        public const string GetCustomerById =
            @"SELECT c.id,c.DNI, c.nseCategoria,c.accountType,c.isTenant FROM t_customer c WHERE c.dni = @id";

        public const string GetCustomerCreditById =
            @"SELECT c.balanceAvailable,c.creditBlocked FROM t_customer_credit c WHERE c.customerId = @id";

    }
}