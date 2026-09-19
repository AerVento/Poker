using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Game.Events
{
    public interface IPlayerEventSource
    {
        string SourceId { get; }
        void Register(PlayerEventRegistry registry);
    }

    public interface IPlayerEventWriter
    {
        // Append using the caller's transaction; implementations must not commit it.
        void Append(PlayerEvent playerEvent);
    }

    public sealed class PlayerEventCodec
    {
        public string SourceId { get; }
        public string TypeId { get; }
        public int Version { get; }
        public Type EventType { get; }
        public Func<PlayerEvent, string> Encode { get; }
        public Func<string, PlayerEvent> Decode { get; }

        public PlayerEventCodec(string sourceId, string typeId, int version, Type eventType,
            Func<PlayerEvent, string> encode, Func<string, PlayerEvent> decode)
        {
            SourceId = sourceId; TypeId = typeId; Version = version;
            EventType = eventType; Encode = encode; Decode = decode;
        }
    }

    public sealed class PlayerEventRegistry
    {
        private readonly Dictionary<(string, string, int), PlayerEventCodec> _readers =
            new Dictionary<(string, string, int), PlayerEventCodec>();
        private readonly Dictionary<Type, PlayerEventCodec> _writers = new Dictionary<Type, PlayerEventCodec>();
        public IEnumerable<Type> EventTypes => _writers.Keys;

        public void Register<T>(string sourceId, string typeId, int version,
            Func<T, string> encode = null, Func<string, T> decode = null) where T : PlayerEvent
        {
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(typeId) || version < 1)
                throw new ArgumentException("Invalid event schema identity.");
            var codec = new PlayerEventCodec(sourceId, typeId, version, typeof(T),
                value => encode != null ? encode((T)value) : JsonSerializer.Serialize((T)value),
                json => decode != null ? decode(json) : JsonSerializer.Deserialize<T>(json));
            _readers.Add((sourceId, typeId, version), codec);
            if (!_writers.TryGetValue(typeof(T), out var previous) || previous.Version < version)
                _writers[typeof(T)] = codec;
        }

        public PlayerEventCodec For(PlayerEvent value)
        {
            if (!_writers.TryGetValue(value.GetType(), out var codec) || codec.SourceId != value.SourceId)
                throw new InvalidOperationException("Unregistered player event: " + value.GetType().FullName);
            return codec;
        }

        public PlayerEvent Read(string source, string type, int version, string json)
        {
            if (!_readers.TryGetValue((source, type, version), out var codec))
                throw new InvalidOperationException($"Unsupported player event schema: {source}/{type}/v{version}");
            var value = codec.Decode(json);
            if (value == null || value.SourceId != source)
                throw new InvalidOperationException("Invalid player event payload.");
            return value;
        }

        // New sources register codecs here; storage and achievement dispatch need no new branches.
        public static PlayerEventRegistry CreateDefault()
        {
            var registry = new PlayerEventRegistry();
            foreach (var source in new IPlayerEventSource[]
            {
                new TableEventSource(), new ShopEventSource(), new InventoryEventSource(), new CollectionEventSource()
            })
            {
                source.Register(registry);
            }
            return registry;
        }
    }
}
