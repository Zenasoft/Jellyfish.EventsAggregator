//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//
//    Copyright (c) Zenasoft
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Newtonsoft.Json;
using System.Reactive.Linq;
using System.Text;
using Microsoft.AspNet.Http;
using System.Threading;

namespace Jellyfish.EventsAggregator
{
    public class EventsAggregatorMiddleware
    {
        private RequestDelegate _next;

        public EventsAggregatorMiddleware(RequestDelegate next)
        {
            _next = next;
        }


        private IStreamDiscovery CreateStreamDiscovery(HttpRequest request)
        {
            var streams = request.Query["streams"];
            if( streams.FirstOrDefault() != null)
            {
                return new StaticStreamDiscovery(streams.Select(uri=>uri.Trim()));
            }

            return new EtcdStreamDiscovery();
        }

        public async Task Invoke(HttpContext ctx)
        {
            var hystrixFormat = false;
            if (ctx.Request.Path != "/jellyfish.stream")
            {
                if (ctx.Request.Path != "/hystrix.stream")
                {
                    await _next.Invoke(ctx);
                    return;
                }
                else
                    hystrixFormat = true;
            }

            var streamDiscovery = CreateStreamDiscovery(ctx.Request);

            var origin = ctx.Request.Headers["Origin"].FirstOrDefault();
            if( origin != null)
                ctx.Response.Headers.Add("Access-Control-Allow-Origin",new string[] { origin} );
                
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Add("Connection", new string[] { "keep-alive" });
            ctx.Response.Headers.Add("Cache-control", new string[] { "no-cache" });
            ctx.Response.StatusCode = 200;
            await ctx.Response.Body.FlushAsync();

            var streamActions = streamDiscovery.GetInstances().Publish().RefCount();
            var streamAdds = streamActions.Where(a => a.ActionType == StreamAction.StreamActionType.ADD);
            var streamRemoves = streamActions.Where(a => a.ActionType == StreamAction.StreamActionType.REMOVE);

            var token = new CancellationTokenSource();
            token = ctx.RequestAborted != null ? CancellationTokenSource.CreateLinkedTokenSource( ctx.RequestAborted, token.Token ) : token;

            var streams = streamAdds.Select(action => SSEProvider.ReceiveSse(action.Uri, ctx.Request, token, streamRemoves))
                                    .Merge();

            IObservable<IDictionary<string, object>> agreg = AggregateStreams(streams);

            //Console.WriteLine("Start request");
            var stop = false;
            var subscription = agreg.Subscribe(async data =>
            {
                try
                {
                    data.Remove("TypeAndName");
                    data.Remove("instanceId");

                    if (hystrixFormat)
                    {
                        data["type"] = "HystrixCommand";
                    }

                    var bytes = Encoding.UTF8.GetBytes(String.Format("data:{0}\n\n", JsonConvert.SerializeObject(data)));
                    ctx.Response.Body.Write(bytes, 0, bytes.Length);
                    
                    await ctx.Response.Body.FlushAsync();
                    //   Console.WriteLine("Send data");
                }
                catch
                {
                    stop = true;
                    //Console.WriteLine("Stop request");
                }
            });

            do
            {
                // TODO remove this on rc2 when issue https://github.com/aspnet/KestrelHttpServer/issues/297 will be closed
                try
                {
                    await ctx.Response.Body.FlushAsync();
                }
                catch
                {
                    stop = true;
                    break;
                }
                // END 
                await Task.Delay(1000);
            }
            while (!stop && !ctx.RequestAborted.IsCancellationRequested);

            //Console.WriteLine("End request");
            if(!ctx.RequestAborted.IsCancellationRequested)
                token.Cancel();

            subscription.Dispose();
        }

        internal static IObservable<IDictionary<string, object>> AggregateStreams(IObservable<IDictionary<string, object>> streams)
        {
            var bycommand = streams.GroupBy(data => data["TypeAndName"]);

            var agreg = bycommand.SelectMany(commanGroup =>
            {
                var sumOfDeltasForAllInstancesForCommand = commanGroup.GroupBy(data => data["instanceId"])
                   .SelectMany(instanceGroup =>
                   {
                       return instanceGroup
                                   //  .TakeWhile( d => d["tombstone"] == null)
                                   .StartWith(new Dictionary<string, object>())
                                   .Buffer(2, 1)
                                   .Where(list => list.Count == 2)
                                   .Select(PreviousAndCurrentToDelta)
                                   .Where(data => data != null && data.Any());
                   })
                   .Scan((IDictionary<string, object>)new Dictionary<string, object>(), SumOfDelta)
                   .Skip(1);

                return sumOfDeltasForAllInstancesForCommand;
            });
            return agreg;
        }

        public static IDictionary<string, object> SumOfDelta(IDictionary<string, object> state, IDictionary<string, object> delta)
        {
            foreach (var key in delta.Keys)
            {
                object existing;
                state.TryGetValue(key, out existing);
                var current = delta[key];
                if (current is long && !key.StartsWith("propertyValue_"))
                {
                    if (existing == null)
                    {
                        existing = 0L;
                    }
                    var v = (long)existing;
                    var d = (long)current;
                    state[key] = v + d;
                }
                else if (current is IDictionary<string, double>)
                {
                    if (existing == null)
                    {
                        state[key] = current;
                    }
                    else
                    {
                        state[key] = SumList((IDictionary<string, double>)existing, (IDictionary<string, double>)current);
                    }
                }
                else
                {
                    state[key] = current;
                }
            }
            return state;
        }

        public static IDictionary<string, object> PreviousAndCurrentToDelta(IList<IDictionary<string, object>> data)
        {
            if (data.Count == 2)
            {
                var previous = data[0];
                var current = data[1];
                return PreviousAndCurrentToDelta(previous, current);
            }
            else
            {
                throw new ArgumentException("Must be list of 2 items");
            }
        }

        private static IDictionary<string, object> PreviousAndCurrentToDelta(IDictionary<string, object> previous, IDictionary<string, object> current)
        {
            if (previous.Count == 0)
            {
                // the first time through it is empty so we'll emit the current to start
                var seed = new Dictionary<string, object>();

                foreach (var key in current.Keys)
                {
                    var currentValue = current[key];
                    if (IsIdentifierKey(key))
                    {
                        seed.Add(key, currentValue);
                        continue;
                    }

                    if ((currentValue is double || currentValue is int || currentValue is long) && !key.StartsWith("propertyValue_"))
                    {
                        // convert all numbers to Long so they are consistent
                        seed.Add(key, Convert.ToInt64(currentValue));
                    }
                    else if (currentValue is IDictionary<string, double>)
                    {
                        // NOTE: we are expecting maps to only contain key/value pairs with values as numbers
                        seed.Add(key, currentValue);
                    }
                    else
                    {
                        seed.Add(key, currentValue);
                    }
                }
                return seed;
            }
            else if (current.Count == 0 || ContainsOnlyIdentifiers(current))
            {
                // we are terminating so the delta we emit needs to remove everything
                var delta = new Dictionary<String, Object>();
                foreach (var key in previous.Keys)
                {
                    var previousValue = previous[key];
                    if (IsIdentifierKey(key))
                    {
                        delta.Add(key, previousValue);
                        continue;
                    }

                    if ((previousValue is double || previousValue is int || previousValue is long) && !key.StartsWith("propertyValue_"))
                    {
                        var previousValueAsNumber = Convert.ToInt64(previousValue);
                        long d = -previousValueAsNumber;
                        delta.Add(key, d);
                    }
                    else if (previousValue is IDictionary<string, double>)
                    {
                        delta.Add(key, ((IDictionary<string, double>)previousValue).ToDictionary(kv=>kv.Key, kv=>-kv.Value));
                    }
                    else
                    {
                        delta.Add(key, previousValue);
                    }
                }
                return delta;
            }
            else
            {
                // we have previous and current so calculate delta
                var delta = new Dictionary<String, Object>();

                foreach (var key in current.Keys)
                {
                    var currentValue = current[key];
                    if (IsIdentifierKey(key))
                    {
                        delta.Add(key, currentValue);
                        continue;
                    }

                    var previousValue = previous[key];
                    if ((previousValue is double || previousValue is int || previousValue is long) && !key.StartsWith("propertyValue_"))
                    {
                        if (previousValue == null)
                        {
                            previousValue = 0;
                        }
                        var previousValueAsNumber = Convert.ToInt64(previousValue);
                        if (currentValue != null)
                        {
                            var currentValueAsNumber = Convert.ToInt64(currentValue);
                            long d = (currentValueAsNumber - previousValueAsNumber);
                            delta.Add(key, d);
                        }
                    }
                    else if (currentValue is IDictionary<string, double>)
                    {
                        if (previousValue == null)
                        {
                            delta.Add(key, currentValue);
                        }
                        else
                        {
                            delta.Add(key, DeltaList((IDictionary<string, double>)currentValue, (IDictionary<string, double>)previousValue));
                        }
                    }
                    else
                    {
                        delta.Add(key,currentValue);
                    }
                }
                return delta;
            }
        }

        private static IDictionary<string, double> SumList(IDictionary<string, double> list1, IDictionary<string, double> list2)
        {
            var result = new Dictionary<string, double>();
            foreach (var kv in list1)
            {
                var other = list2[kv.Key];
                result.Add(kv.Key, kv.Value + other);
            }
            return result;
        }

        private static IDictionary<string, double> DeltaList(IDictionary<string, double> list1, IDictionary<string, double> list2)
        {
            var result = new Dictionary<string, double>();
            foreach(var kv in list1)
            {
                var other = list2[kv.Key];
                result.Add(kv.Key, kv.Value - other);
            }
            return result;
        }

        private static bool ContainsOnlyIdentifiers(IDictionary<string, object> m)
        {
            foreach (var k in m.Keys)
            {
                if (!IsIdentifierKey(k))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsIdentifierKey(String key)
        {
            return key.Equals("InstanceKey") || key.Equals("TypeAndName") || key.Equals("instanceId") || key.Equals("currentTime") || key.Equals("name") || key.Equals("type");
        }
    }
}
