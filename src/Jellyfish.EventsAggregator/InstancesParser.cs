﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Jellyfish.EventsAggregator
{
    internal class Item
    {
        public string Ip;
        public int Port;
        public bool Enabled;
        public string Id;
    }

    internal static class InstancesParser
    {
        private static List<Item> _items;
        private static Item _current;
        private static bool _versionEnabled;

        public static IEnumerable<Item> GetInstances(string json)
        {
            if(json== null)
                return Enumerable.Empty<Item>();

            _items = new List<Item>();
            var jarray = JToken.Parse(json);
            VisitArray(jarray.Value<JArray>());
            return _items;
        }

        private static void VisitObject(JObject jobj)
        {
            // Check status before parsing properties to don't care of properties order
            var enabled = jobj.Property("status"); // only for version
            if( enabled != null)
                _versionEnabled = enabled.Value.ToString() != "0";

            foreach (var property in jobj.Properties())
            {
                VisitProperty(property);
            }
        }

        private static void VisitArray(JArray array, bool isInstance=false)
        {
            for (var index = 0; index < array.Count; index++)
            {
                if (isInstance)
                {
                    _current = new Item { Enabled = _versionEnabled };
                    _items.Add(_current);
                }

                var data = array[index];
                VisitObject(data.Value<JObject>());
            }
        }

        private static void VisitProperty(JProperty prop)
        {
            switch (prop.Name)
            {
                case "enabled":
                    _current.Enabled &= prop.Value.ToString() == "True";
                    break;
                case "versions":
                case "ports":
                case "instances":
                    VisitArray(prop.Value.Value<JArray>(), prop.Name=="instances");
                    break;
                case "id":
                    _current.Id = prop.Value.ToString();
                    break;
                case "ip":
                    _current.Ip = prop.Value.ToString();
                    break;
                case "boundedPort":
                    _current.Port = Int32.Parse(prop.Value.ToString());
                    break;
            }
        }
    }
}