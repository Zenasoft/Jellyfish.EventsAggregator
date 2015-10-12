using Jellyfish.EventsAggregator;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Test
{
    public class AggregationTests
    {
        private string sseAsString = "{\"type\":\"ServiceCommand\",\"name\":\"Sample.HttpCommand.Commands.MyCommand\",\"group\":\"MyCommand\",\"currentTime\":1017269141399573,\"isCircuitBreakerOpen\":false,\"errorPercentage\":0,\"errorCount\":1,\"requestCount\":2,\"rollingCountBadRequests\":0,\"rollingCountExceptionsThrown\":0,\"rollingCountFailure\":0,\"rollingCountFallbackFailure\":0,\"rollingCountFallbackRejection\":0,\"rollingCountFallbackSuccess\":0,\"rollingCountResponsesFromCache\":0,\"rollingCountSemaphoreRejected\":0,\"rollingCountShortCircuited\":0,\"rollingCountSuccess\":0,\"rollingCountThreadPoolRejected\":0,\"rollingCountTimeout\":0,\"currentConcurrentExecutionCount\":0,\"rollingMaxConcurrentExecutionCount\":1,\"latencyExecute_mean\":0,\"latencyExecute\":{\"0\":1.0,\"25\":0.0,\"50\":5.0,\"75\":0.0,\"90\":10.0,\"95\":0.0,\"99\":0.0,\"99.5\":0.0,\"100\":0.0},\"latencyTotal_mean\":0,\"latencyTotal\":{\"0\":0.0,\"25\":0.0,\"50\":0.0,\"75\":0.0,\"90\":0.0,\"95\":0.0,\"99\":0.0,\"99.5\":0.0,\"100\":0.0},\"propertyValue_circuitBreakerRequestVolumeThreshold\":320,\"propertyValue_circuitBreakerSleepWindowInMilliseconds\":80000,\"propertyValue_circuitBreakerErrorThresholdPercentage\":800,\"propertyValue_circuitBreakerForceOpen\":false,\"propertyValue_circuitBreakerForceClosed\":false,\"propertyValue_circuitBreakerEnabled\":true,\"propertyValue_executionTimeoutInMilliseconds\":16000,\"propertyValue_executionIsolationSemaphoreMaxConcurrentRequests\":160,\"propertyValue_fallbackIsolationSemaphoreMaxConcurrentRequests\":160,\"propertyValue_metricsRollingStatisticalWindowInMilliseconds\":160000,\"reportingHosts\":1,\"instanceId\":\"192.168.1.22\"}";

        [Fact]
        public void TestSumValues()
        {
            var sse = SSEProvider.DeserializeData(sseAsString);

            var s = new Startup();
       //     var r = s.PreviousAndCurrentToDelta(sse, sse);
        }
    }
}
