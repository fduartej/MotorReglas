using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Contoso.Modules.Loan.Entities
{
    public class CustomerCredit
    {
        public Customer? Customer { get; set; }
        public decimal BalanceAvailable { get; set; }
        public bool CreditBlocked { get; set; }

    }
}