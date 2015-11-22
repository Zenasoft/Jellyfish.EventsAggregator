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
using Microsoft.AspNet.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfish.EventsAggregator
{
    public class SSEProvider
    {
        class JsonContext
        {
            public string Property { get; set; }
            public Action<string, object> Setter { set; private get; }

            public void Set(object v)
            {
                Setter(Property, v);
            }
        }

        public static IObservable<IDictionary<string, object>> ReceiveSse(string address, HttpRequest origin, CancellationTokenSource token, IObservable<StreamAction> streamRemoved)
        {
            return Observable.Create<IDictionary<string, object>>((Func<IObserver<IDictionary<string, object>>, Task>)(async (IObserver<IDictionary<string, object>> observer) =>
            {
                var subscription = streamRemoved.Where(a => a.Uri == address).Subscribe(o => {
                    token.Cancel();
                });

                var client = new HttpClient();
                var builder = new UriBuilder(address);
                if (origin != null)
                    builder.Query = origin.QueryString.Value?.Substring(1);
                var uri = builder.Uri;

                await Task.Delay(TimeSpan.FromSeconds(1));

                while (true)
                {
                    try
                    {
                        //Console.WriteLine("Get info for " + address);

                        using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token.Token).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();

                            var stream = new StreamReader(await response.Content.ReadAsStreamAsync());
                            while (!token.IsCancellationRequested)
                            {
                                var line = await stream.ReadLineAsync();
                                if (line.Length > 0 && line.StartsWith("data: "))
                                {
                                    var data = DeserializeData(line.Substring("data: ".Length));
                                    data["instanceId"] =  uri.Authority.ToString();
                                    //Console.WriteLine($"{uri.Authority} -> {data["requestCount"]}");
                                    observer.OnNext(data);
                                }

                                await Task.Yield();
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        if (IsSocketException(ex) || token.IsCancellationRequested) break;
                        //Console.WriteLine("Error waiting 10 sec for " + address);
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }

                //Console.WriteLine("Completed for " + address);
                observer.OnCompleted();
            }));
        }

        private static bool IsSocketException(Exception ex)
        {
            while(ex != null)
            {
                if (ex is System.Net.WebSockets.WebSocketException) return true;
                ex = ex.InnerException;
            }
            return false;
        }

        public static IDictionary<string, object> DeserializeData(string line)
        {
            var data = new Dictionary<string, object>();
            JsonTextReader reader = new JsonTextReader(new StringReader(line));

            // Very simple parser
            var stack = new Stack<JsonContext>();
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        if (stack.Count == 0)
                        {
                            stack.Push(new JsonContext { Setter = (p, v) => data[p] = v });
                        }
                        else
                        {
                            var dic = new Dictionary<string, double>();
                            stack.Peek().Set(dic);
                            stack.Push(new JsonContext { Setter = (p, v) => dic[p] = Convert.ToDouble(v) });
                        }
                        break;
                    case JsonToken.PropertyName:
                        var property = (string)reader.Value;
                        stack.Peek().Property = property;
                        break;
                    case JsonToken.Boolean:
                    case JsonToken.String:
                    case JsonToken.Integer:
                    case JsonToken.Float:
                        //                                case JsonToken.Date:
                        stack.Peek().Set(reader.Value);
                        break;
                    case JsonToken.EndObject:
                        stack.Pop();
                        break;
                }
            }

            data["TypeAndName"] = (string)data["type"] + data["name"];
            return data;
        }
    }
}

