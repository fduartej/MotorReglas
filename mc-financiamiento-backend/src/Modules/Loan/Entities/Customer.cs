namespace Contoso.Modules.Loan.Entities
{
    public class Customer
    {
        public string? Id { get; set; }
        public string? DNI { get; set; }
        public string? NseCategoria { get; set; }
        public string? AccountType { get; set; }
        public bool IsTenant { get; set; }
    }
}