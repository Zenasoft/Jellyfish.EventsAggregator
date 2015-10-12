using System;

namespace Jellyfish.EventsAggregator
{
    interface IStreamDiscovery
    {
        IObservable<StreamAction> GetInstances();
    }
}