namespace Game.Network
{
    // Monotonic time is supplied by the caller, keeping expiry deterministic and testable.
    public sealed class ClientSnapshotCache<T>
    {
        private readonly double _lifetime;
        private T _value;
        private double _expiresAt;
        private bool _hasValue;
        public ClientSnapshotCache(double lifetime) => _lifetime = lifetime;
        public void Store(T value, double now)
        {
            _value = value;
            _expiresAt = now + _lifetime;
            _hasValue = true;
        }
        public bool TryGet(double now, out T value)
        {
            value = _value;
            return _hasValue && now < _expiresAt;
        }
        public void Clear()
        {
            _value = default;
            _hasValue = false;
        }
    }
}
