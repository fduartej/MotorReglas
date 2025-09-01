using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Integration.GoRules
{
    public class GoRulesIntegration
    {
        private readonly string? _endpointUrl;
        private readonly string? _accessTokenKey;
        private readonly string? _accessTokenValue;
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoRulesIntegration> _logger;

        public GoRulesIntegration(IConfiguration configuration, HttpClient httpClient, ILogger<GoRulesIntegration> logger)
        {
            // Leer las configuraciones desde appsettings.json
            _endpointUrl = configuration["Integration:GoRules.Endpoint.001"];
            _accessTokenKey = configuration["Integration:GoRules.Endpoint.001.X-Access-Token-Key"];
            _accessTokenValue = configuration["Integration:GoRules.Endpoint.001.X-Access-Token-value"];
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> CallRulesEngineAsync(string requestBody)
        {
            try
            {
                if (string.IsNullOrEmpty(_endpointUrl))
                {
                    throw new ArgumentNullException(nameof(_endpointUrl), "Endpoint URL cannot be null or empty.");
                }

                // Log del requestBody
                _logger.LogInformation("Sending request to GoRules. Endpoint: {Endpoint}, RequestBody: {RequestBody}", _endpointUrl, requestBody);

                // Crear la solicitud HTTP
                using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl)
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
                };

                // Agregar encabezados
                request.Headers.Add("Accept", "application/json");
                if (!string.IsNullOrEmpty(_accessTokenKey) && !string.IsNullOrEmpty(_accessTokenValue))
                {
                    request.Headers.Add(_accessTokenKey, _accessTokenValue);
                }

                // Enviar la solicitud
                using var response = await _httpClient.SendAsync(request);

                // Leer el contenido de la respuesta
                var responseContent = await response.Content.ReadAsStringAsync();

                // Log de la respuesta
                _logger.LogInformation("Received response from GoRules. StatusCode: {StatusCode}, ResponseBody: {ResponseBody}", response.StatusCode, responseContent);

                // Verificar si la respuesta fue exitosa
                response.EnsureSuccessStatusCode();

                return responseContent;
            }
            catch (Exception ex)
            {
                // Log del error
                _logger.LogError(ex, "Error while calling GoRules endpoint.");
                throw new Exception($"Error en GoRulesIntegration: {ex.Message}", ex);
            }
        }
    }
}