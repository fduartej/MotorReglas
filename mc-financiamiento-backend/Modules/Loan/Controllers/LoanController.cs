using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Contoso.Modules.Loan.DTOs;
using Contoso.Modules.Loan.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using System;

namespace Contoso.Modules.Loan.Controllers
{
    [ApiController]
    [Route("loan")]
    public class LoanController : ControllerBase
    {
        private readonly LoanService _loanService;
        private readonly ILogger<LoanController> _logger;

        public LoanController(LoanService loanService,
            ILogger<LoanController> logger)
        {
            _loanService = loanService;
            _logger = logger;
        }

        [HttpPost]
        [Route("{dni}/evaluation")]
        [ProducesResponseType(typeof(LoanApplyResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ApplyLoan([FromRoute] string dni, [FromBody] LoanApplyRequestDto loan)
        {
            _logger.LogInformation("Starting loan application for DNI: {DNI}", dni); // Log start of method
            try
            {
                loan.DNI = dni;
                var result = await _loanService.ApplyLoanAsync(loan);
                _logger.LogInformation("Loan application successful for DNI: {DNI}", dni); // Log success
                return CreatedAtAction(nameof(ApplyLoan), null, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while applying for loan for DNI: {DNI}", dni); // Log error
                return BadRequest(new { Message = ex.Message });
            }
        }


    }
}