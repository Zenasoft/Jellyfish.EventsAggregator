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
namespace Jellyfish.EventsAggregator
{
    public class StreamAction
    {
        public enum StreamActionType
        {
            ADD,
            REMOVE
        }

        public StreamActionType ActionType
        {
            get;
            private set;
        }

        public string Uri
        {
            get;
            private set;
        }

        public StreamAction(StreamActionType type, string uri)
        {
            Uri = uri;
            ActionType = type;
        }

        public override int GetHashCode()
        {
            int prime = 31;
            int result = 1;
            result = prime * result + (int)ActionType;
            result = prime * result + ((Uri == null) ? 0 : Uri.GetHashCode());
            return result;
        }

        public override bool Equals(object obj)
        {
            var other = obj as StreamAction;
            if (obj == null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return this.ActionType == other.ActionType && this.Uri == other.Uri;
        }
    }
}