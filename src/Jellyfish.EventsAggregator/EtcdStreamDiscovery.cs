using System;
using System.Linq;
using System.Reactive.Linq;
using Draft;
using Draft.Responses;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Reactive.Disposables;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace Jellyfish.EventsAggregator
{
    class EtcdStreamDiscovery : IStreamDiscovery
    {
        private IEtcdClient _etcd;
        private Dictionary<string, string> _uris = new Dictionary<string, string>();
        private string _key;
        private long _processing;

        public EtcdStreamDiscovery()
        {
            _etcd = Etcd.ClientFor(new Uri("http://192.168.1.44:2379"));
#if DEBUG
            _key = "/jellyfish/runtime/test/services";
#else
            _key = "/jellyfish/runtime/" + Environment.MachineName + "/services";
#endif
        }

        public IObservable<StreamAction> GetInstances()
        {
            IDisposable subscription;
            return Observable.Create<StreamAction>(async (observer) =>
            {
                try
                {
                    var result = await _etcd.GetKey(_key).WithRecursive(true);
                    ProcessInstances(observer, result);
                }
                catch (Draft.Exceptions.KeyNotFoundException)
                {
                }

                subscription = _etcd.Watch(_key).WithRecursive(true).Subscribe((IKeyEvent e) =>
                {
                    if (Interlocked.CompareExchange(ref _processing, 1, 0) == 0)
                    {
                        ProcessInstances(observer, e);
                        Interlocked.Exchange(ref _processing, 0);
                    }
                });

                return Disposable.Create(() => { subscription.Dispose(); });
            });
        }

        private void ProcessInstances(IObserver<StreamAction> observer, IKeyEvent e)
        {
            IEnumerable<Item> runningInstances = InstancesParser.GetInstances(e.Data?.RawValue);

            // Check instances
            foreach (var instance in runningInstances)
            {
                string address;
                if (_uris.TryGetValue(instance.Id, out address))
                {
                    if (instance.Enabled == false)
                    {
                        observer.OnNext(new StreamAction(StreamAction.StreamActionType.REMOVE, address));
                        _uris.Remove(instance.Id);
                    }
                }
                else
                {
                    address = String.Format("http://{0}:{1}/jellyfish.stream", instance.Ip, instance.Port);
                    _uris.Add(instance.Id, address);
                    observer.OnNext(new StreamAction(StreamAction.StreamActionType.ADD, address));
                }
            }

            // Remove deleted instances
            // Check instance in cache but not running
            var instancesToRemove = (from kv in _uris
                                     where !runningInstances.Any(i => i.Id == kv.Key)
                                     select kv).ToList();

            foreach (var kv in instancesToRemove)
            {
                if( _uris.Remove(kv.Key))
                    observer.OnNext(new StreamAction(StreamAction.StreamActionType.REMOVE, kv.Value));
            }
        }
    }
}