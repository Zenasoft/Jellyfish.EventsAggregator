using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace Jellyfish.EventsAggregator
{
    class StaticStreamDiscovery : IStreamDiscovery
    {
        private string[] _uris;

        public StaticStreamDiscovery(IEnumerable<string> uris)
        {
            _uris = uris.ToArray();
        }
        
        public IObservable<StreamAction> GetInstances()
        {
            return _uris.Select(u => new StreamAction(StreamAction.StreamActionType.ADD, u)).ToObservable();
        }
    }
}