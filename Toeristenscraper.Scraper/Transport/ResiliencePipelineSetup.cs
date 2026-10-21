using Microsoft.Extensions.Http.Resilience;
using Polly;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net;
using Toeristenscraper.Scraper.Configuration;

namespace Toeristenscraper.Scraper.Transport
{
    public static class ResiliencePipelineSetup
    {
        public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> pipelineBuilder, ResilienceHandlerContext context)
        {
            ResilienceOptions settings = context.ServiceProvider
            .GetRequiredService<IOptions<ScraperOptions>>().Value.Resilience;

            // retry bij 429 (too many requests)
            var rateLimitRetry = new HttpRetryStrategyOptions();
            rateLimitRetry.MaxRetryAttempts = settings.RateLimitMaxRetries;
            rateLimitRetry.Delay = TimeSpan.FromSeconds(settings.RateLimitDelaySeconds);
            rateLimitRetry.BackoffType = DelayBackoffType.Constant;
            rateLimitRetry.UseJitter = false;
            rateLimitRetry.ShouldRetryAfterHeader = true;
            
            rateLimitRetry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(response => response.StatusCode == HttpStatusCode.TooManyRequests);
            pipelineBuilder.AddRetry(rateLimitRetry);


            // retry bij tijdelijke fouten
            var transientErrorsRetry = new HttpRetryStrategyOptions();
            transientErrorsRetry.MaxRetryAttempts = settings.MaxRetries;
            transientErrorsRetry.Delay = TimeSpan.FromSeconds(settings.RetryDelaySeconds);
            transientErrorsRetry.ShouldRetryAfterHeader = false;
            transientErrorsRetry.BackoffType = DelayBackoffType.Constant;
            transientErrorsRetry.UseJitter = false;

            transientErrorsRetry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(response => (int)response.StatusCode >= 500) // alle 5xx fouten
                .Handle<HttpRequestException>() 
                .Handle<TimeoutRejectedException>(); // retry strategie wordt uitgevoerd na een timeout exception
            pipelineBuilder.AddRetry(transientErrorsRetry);

           
            // circuit-breaker (alleen 429) 
            var circuitBreaker = new CircuitBreakerStrategyOptions<HttpResponseMessage>();
            circuitBreaker.FailureRatio = settings.CircuitFailureRatio;
            circuitBreaker.MinimumThroughput = settings.CircuitMinimumThroughput;
            circuitBreaker.SamplingDuration = TimeSpan.FromSeconds(settings.CircuitSamplingSeconds);

            circuitBreaker.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(response => response.StatusCode == HttpStatusCode.TooManyRequests); // alleen 429
            pipelineBuilder.AddCircuitBreaker(circuitBreaker);

            // timeout
            pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(settings.TimeoutSeconds));





        }
    }
}
