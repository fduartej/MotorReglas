using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Contoso.Modules.Loan.Entities
{
    public class CustomerLoan
    {
        public Customer? Customer { get; set; }
        public decimal loanAmountRequested { get; set; }
        public decimal? loanAmount { get; set; }
        public decimal? interestRate { get; set; }
        public int? loanTerm { get; set; }
    }
}