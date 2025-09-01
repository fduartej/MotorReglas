namespace Contoso.Modules.Loan.DTOs
{
    public class LoanApplyRequestDto
    {
        public string? DNI { get; set; }
        public decimal LoanAmountRequested { get; set; }
        public int LoanTerm { get; set; }

    }

    public class LoanApplyResponseDto
    {
        public bool? Success { get; set; }
        public EvaluationDto? Evaluation { get; set; }
    }

    public class EvaluationDto
    {
        public string? Performance { get; set; }
        public ResultDto? Result { get; set; }
    }

    public class ResultDto
    {
        public Dictionary<string, object>? CreditDecision { get; set; } // Manejo dinámico
        public Dictionary<string, object>? Validations { get; set; } // Manejo dinámico
    }
}