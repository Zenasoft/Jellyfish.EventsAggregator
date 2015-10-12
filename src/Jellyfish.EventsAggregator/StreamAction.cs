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