using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Infrastructure.Helpers
{
    public  class PollyHelper
    {
        public static AsyncRetryPolicy GetRetryPolicy(int retryCount, TimeSpan delay)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(retryCount, retryAttempt => delay);
        }

        public static CircuitBreakerPolicy GetCircuitBreakerPolicy(int exceptionsAllowedBeforeBreaking, TimeSpan durationOfBreak)
        {
            return Policy
                .Handle<Exception>()
                .CircuitBreaker(exceptionsAllowedBeforeBreaking, durationOfBreak);
        }
    }
}