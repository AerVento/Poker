using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Content
{
    /// <summary>Which packages must move to their latest version before use, and the packages the use involves.</summary>
    public sealed class RemoteContentPlan
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }
        /// <summary>Packages that use their latest version (downloaded first when needed); all others use the local version.</summary>
        public IReadOnlyCollection<string> Updates { get; private set; } = Array.Empty<string>();
        /// <summary>The root and every package it depends on, directly or not, under the chosen versions.</summary>
        public IReadOnlyList<string> Closure { get; private set; } = Array.Empty<string>();

        internal static RemoteContentPlan Ok(HashSet<string> updates, List<string> closure) =>
            new RemoteContentPlan { Success = true, Updates = updates, Closure = closure };

        internal static RemoteContentPlan Fail(string error) => new RemoteContentPlan { Error = error };
    }

    /// <summary>
    /// Pure dependency rules. The site holds only the latest build, whose packages are mutually compatible, so each
    /// package uses either its local version or its latest one. A violated dependency moves the outdated side to its
    /// latest version; the set only grows, and "everything latest" always satisfies, so the computation ends.
    /// </summary>
    public static class RemoteContentPlanner
    {
        // Far above any real dependency graph; only a broken lookup could reach it.
        private const int MaxRounds = 10000;

        private enum Step { Done, Changed, Failed }

        /// <param name="local">The usable local record of an address, or null.</param>
        /// <param name="latest">The record of an address in the accepted catalog, or null.</param>
        public static RemoteContentPlan ForUse(string root, Func<string, RemoteContentRecord> local, Func<string, RemoteContentRecord> latest)
        {
            var updates = new HashSet<string>(StringComparer.Ordinal);
            for (var round = 0; round < MaxRounds; round++)
            {
                var walk = new Walk(updates, local, latest);
                switch (walk.Visit(root))
                {
                    case Step.Done: return RemoteContentPlan.Ok(updates, walk.Closure);
                    case Step.Failed: return RemoteContentPlan.Fail(walk.Error);
                }
            }
            return RemoteContentPlan.Fail("Dependency computation did not settle: " + root);
        }

        /// <summary>
        /// A background update of target: also moves the dependencies it needs, and every local package that the
        /// moved packages would break, so the browser's records stay compatible as a whole.
        /// </summary>
        public static RemoteContentPlan ForUpdate(string target, IEnumerable<string> localAddresses,
            Func<string, RemoteContentRecord> local, Func<string, RemoteContentRecord> latest)
        {
            if (latest(target) == null) return RemoteContentPlan.Fail("Not in the content catalog: " + target);
            var updates = new HashSet<string>(StringComparer.Ordinal) { target };
            var locals = localAddresses.Distinct(StringComparer.Ordinal).ToArray();
            RemoteContentRecord Pick(string address) => updates.Contains(address) ? latest(address) : local(address);
            for (var round = 0; round < MaxRounds; round++)
            {
                var changed = false;
                // Downward: the latest versions being adopted must find their dependencies.
                foreach (var address in updates.ToArray())
                {
                    foreach (var dependency in Dependencies(latest(address)))
                    {
                        if (Satisfies(Pick(dependency.Address), dependency)) continue;
                        if (updates.Contains(dependency.Address) || !Satisfies(latest(dependency.Address), dependency))
                            return RemoteContentPlan.Fail(Inconsistent(address, dependency));
                        updates.Add(dependency.Address);
                        changed = true;
                    }
                }
                // Upward: a local package depending on a moved package must still be satisfied.
                foreach (var address in locals)
                {
                    if (updates.Contains(address) || latest(address) == null) continue;
                    if (Dependencies(local(address)).Any(d => updates.Contains(d.Address) && !Satisfies(latest(d.Address), d)))
                    {
                        updates.Add(address);
                        changed = true;
                    }
                }
                if (!changed) return RemoteContentPlan.Ok(updates, updates.ToList());
            }
            return RemoteContentPlan.Fail("Dependency computation did not settle: " + target);
        }

        private sealed class Walk
        {
            private readonly HashSet<string> _updates;
            private readonly Func<string, RemoteContentRecord> _local;
            private readonly Func<string, RemoteContentRecord> _latest;
            private readonly Dictionary<string, bool> _visited = new Dictionary<string, bool>(StringComparer.Ordinal);
            private readonly List<string> _stack = new List<string>();
            public readonly List<string> Closure = new List<string>();
            public string Error;

            public Walk(HashSet<string> updates, Func<string, RemoteContentRecord> local, Func<string, RemoteContentRecord> latest)
            {
                _updates = updates;
                _local = local;
                _latest = latest;
            }

            private RemoteContentRecord Pick(string address) => _updates.Contains(address) ? _latest(address) : _local(address);

            public Step Visit(string address)
            {
                var record = Pick(address);
                if (record == null)
                {
                    // Only the root gets here: a dependency without a usable version is a violation of its parent.
                    if (!_updates.Contains(address) && _latest(address) != null)
                    {
                        _updates.Add(address);
                        return Step.Changed;
                    }
                    Error = "Not in the content catalog: " + address;
                    return Step.Failed;
                }
                _visited[address] = false;
                _stack.Add(address);
                foreach (var dependency in Dependencies(record))
                {
                    var target = dependency.Address;
                    if (!Satisfies(Pick(target), dependency))
                    {
                        if (!_updates.Contains(target) && Satisfies(_latest(target), dependency)) _updates.Add(target);
                        else if (!_updates.Contains(address)) _updates.Add(address);
                        else
                        {
                            Error = Inconsistent(address, dependency);
                            return Step.Failed;
                        }
                        return Step.Changed;
                    }
                    if (_visited.TryGetValue(target, out var finished))
                    {
                        if (finished) continue;
                        // Local records from different builds formed a cycle; latest versions never do.
                        var added = false;
                        foreach (var member in _stack.Skip(_stack.IndexOf(target)))
                            added |= _updates.Add(member);
                        if (added) return Step.Changed;
                        Error = "Dependency cycle in the content catalog at " + target;
                        return Step.Failed;
                    }
                    var step = Visit(target);
                    if (step != Step.Done) return step;
                }
                _stack.RemoveAt(_stack.Count - 1);
                _visited[address] = true;
                Closure.Add(address);
                return Step.Done;
            }
        }

        private static IEnumerable<RemoteContentDependency> Dependencies(RemoteContentRecord record) =>
            record?.Dependencies?.Where(d => d != null) ?? Enumerable.Empty<RemoteContentDependency>();

        private static bool Satisfies(RemoteContentRecord record, RemoteContentDependency dependency) =>
            record != null && SemanticVersion.Satisfies(record.Version, dependency.Version);

        private static string Inconsistent(string address, RemoteContentDependency dependency) =>
            "Content catalog is inconsistent: " + address + " requires " + dependency.Address + " " + dependency.Version;
    }
}
