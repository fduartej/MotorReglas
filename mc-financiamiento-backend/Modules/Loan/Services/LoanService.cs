using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contoso.Modules.Loan.DTOs;
using Contoso.Modules.Loan.Entities;
using Contoso.Modules.Loan.Repositories;
using Contoso.Modules.Insight.Repositories;
using Infrastructure.Helpers;
using Integration.GoRules;
using System.Text.Json;
using Contoso.Modules.Loan.Config;
using Microsoft.Extensions.Options;

namespace Contoso.Modules.Loan.Services
{
    public class LoanService
    {
        private readonly CustomerRepository _customerRepository;
        private readonly CustomerHistoryPreCalculatedRepository _customerHistoryPreCalculatedRepository;

        private readonly GoRulesIntegration _goRulesIntegration;
        private readonly PollyHelper _pollyHelper;
        private readonly ILogger<LoanService> _logger;
        private readonly LoanConfig _loanConfig;

        public LoanService(CustomerRepository customerRepository,
                CustomerHistoryPreCalculatedRepository customerHistoryPreCalculatedRepository,
                GoRulesIntegration goRulesIntegration,
                 PollyHelper pollyHelper, ILogger<LoanService> logger,
                  IOptions<LoanConfig> loanConfig)
        {
            _customerRepository = customerRepository;
            _customerHistoryPreCalculatedRepository = customerHistoryPreCalculatedRepository;
            _goRulesIntegration = goRulesIntegration;
            _pollyHelper = pollyHelper;
            _logger = logger;
            _loanConfig = loanConfig.Value;
        }

        public async Task<LoanApplyResponseDto> ApplyLoanAsync(LoanApplyRequestDto loan)
        {
            _logger.LogInformation("Starting loan application process for DNI: {DNI}", loan.DNI); // Log start
            try
            {
                var currentPeriod = DateTime.Now.ToString("yyyyMM");
                _logger.LogDebug("Current period: {Period}", currentPeriod);

                var customer = await _customerRepository.GetCustomerByIdAsync(loan.DNI);
                var customerCredit = await _customerRepository.GetCustomerCreditByIdAsync(customer.Id);
                var customerHistory = await _customerHistoryPreCalculatedRepository.GetCustomerHistoryPreCalculatedAsync(loan.DNI, currentPeriod);

                var historicalPreCalculated = customerHistory.ToDictionary(
                    history => history.Variable,
                    history =>
                    {
                        object convertedValue = history.VariableType switch
                        {
                            "N" => int.TryParse(history.VariableValue, out var intValue) ? intValue : 0,
                            "S" => history.VariableValue,
                            "D" => decimal.TryParse(history.VariableValue, out var decimalValue) ? Math.Round(decimalValue, 2) : 0m,
                            "B" => history.VariableValue == "1",
                            _ => history.VariableValue
                        };

                        return convertedValue;
                    }
                );

                // Construir el request
                var request = new
                {
                    decision = _loanConfig.Decision,
                    modelo = _loanConfig.Modelo,
                    inputData = new
                    {
                        customer = new
                        {
                            dni = customer.DNI,
                            nseCategoria = customer.NseCategoria,
                            accountType = customer.AccountType,
                            isTenant = customer.IsTenant
                        },
                        transactionData = new
                        {
                            balanceAvailable = customerCredit.BalanceAvailable,
                            creditBlocked = customerCredit.CreditBlocked,
                            loanAmountRequested = loan.LoanAmountRequested,
                            loanTerm = loan.LoanTerm
                        },
                        historicalPreCalculated = historicalPreCalculated
                    }
                };

                string jsonPayload = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                _logger.LogDebug("Payload sent to GoRules: {Payload}", jsonPayload);

                var responseString = await _goRulesIntegration.CallRulesEngineAsync(jsonPayload);

                _logger.LogDebug("Response received from GoRules: {Response}", responseString);

                var loanApplyResponseDto = JsonSerializer.Deserialize<LoanApplyResponseDto>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                });

                _logger.LogInformation("Loan application process completed successfully for DNI: {DNI}", loan.DNI);

                return loanApplyResponseDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the loan application for DNI: {DNI}", loan.DNI);
                throw;
            }
        }
    }
}