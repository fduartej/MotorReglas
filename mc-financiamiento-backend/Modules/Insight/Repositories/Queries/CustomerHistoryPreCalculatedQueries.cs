using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Contoso.Modules.Insight.Repositories.Queries
{
    public static class CustomerHistoryPreCalculatedQueries
    {
        public const string LisCustomerPreCalculedVariable =
            @"SELECT tc.customerId,tc.period,tc.frequency,tc.variable,tc.variableValue,tc.variableType
                FROM t_customer_history_precalculated tc
            WHERE tc.customerId = @customerId And tc.period = @period";

    }
}