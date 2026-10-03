using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;

namespace Game.Network.Achievements
{
    // Owned by the persistent NetworkRoom, not the panel. Nothing is written to disk.
    public sealed class AchievementClientCache
    {
        private string _server;
        private string _catalogRevision;
        private string _expectedCatalogRevision;
        // The server-scoped catalog is safe to reuse because hidden definitions are always masked.
        // Revealed definitions live only in the account-scoped states cleared by ClearSession.
        private AchievementDefinitionView[] _definitions;
        private Dictionary<string, AchievementStateView> _states = new Dictionary<string, AchievementStateView>();
        public string AccountId { get; private set; }
        public bool HasCatalog => _definitions != null;
        public bool HasStates { get; private set; }
        public bool IsDirty { get; private set; } = true;
        public long ChangeVersion { get; private set; }
        public long StateRevision { get; private set; }

        public void BeginSession(string server, string accountId)
        {
            if (_server != server)
            {
                _server = server;
                _definitions = null;
                _catalogRevision = null;
            }
            ClearSession();
            AccountId = accountId;
        }

        public void ClearSession()
        {
            AccountId = null;
            _states.Clear();
            _expectedCatalogRevision = null;
            HasStates = false;
            IsDirty = true;
            ChangeVersion++;
            StateRevision = 0;
        }

        public void MarkDirty()
        {
            ChangeVersion++;
            IsDirty = true;
        }

        public bool StoreCatalog(AchievementDefinitionView[] definitions, string revision)
        {
            if (!string.IsNullOrEmpty(_expectedCatalogRevision) && _expectedCatalogRevision != revision) return false;
            _definitions = definitions.ToArray();
            _catalogRevision = revision;
            return true;
        }

        public bool StoreStates(AchievementStateView[] states, string catalogRevision, long revision, long requestedAtVersion)
        {
            // A claim response may have already advanced the local revision while this page set was in flight.
            if (revision < StateRevision) return false;
            var next = states.ToDictionary(s => s.Id, StringComparer.Ordinal);
            foreach (var pair in _states)
                if (next.TryGetValue(pair.Key, out var value)) next[pair.Key] = PreserveCompletion(value, pair.Value);
            _states = next;
            StateRevision = revision;
            HasStates = true;
            IsDirty = requestedAtVersion != ChangeVersion;
            _expectedCatalogRevision = catalogRevision;
            if (_catalogRevision != catalogRevision) _definitions = null;
            return true;
        }

        public void ApplyUnlocked(AchievementView view)
        {
            var state = new AchievementStateView { Id = view.Id, Progress = view.Progress,
                CompletedAt = view.CompletedAt, ClaimedAt = view.ClaimedAt,
                Reward = view.Reward,
                HasRevealedDefinition = view.Hidden,
                RevealedDefinition = view.Hidden ? new AchievementDefinitionView
                {
                    Id = view.Id,
                    Name = view.Name,
                    Description = view.Description,
                    Category = view.Category,
                    IconPath = view.IconPath,
                    Target = view.Target,
                    Hidden = true,
                    Reward = view.Reward
                } : default };
            _states.TryGetValue(view.Id, out var prior);
            _states[view.Id] = PreserveCompletion(state, prior);
            MarkDirty();
        }

        public void ApplyClaim(AchievementStateView state, long revision)
        {
            _states.TryGetValue(state.Id, out var prior);
            _states[state.Id] = PreserveCompletion(state, prior);
            StateRevision = Math.Max(StateRevision, revision);
            ChangeVersion++;
        }

        public void ApplyClaims(AchievementStateView[] states, long revision)
        {
            foreach (var state in states ?? Array.Empty<AchievementStateView>())
            {
                _states.TryGetValue(state.Id, out var prior);
                _states[state.Id] = PreserveCompletion(state, prior);
            }
            StateRevision = Math.Max(StateRevision, revision);
            ChangeVersion++;
        }

        private static AchievementStateView PreserveCompletion(AchievementStateView value, AchievementStateView prior)
        {
            if (prior.CompletedAt > 0 && value.CompletedAt == 0) value = prior;
            value.ClaimedAt = Math.Max(value.ClaimedAt, prior.ClaimedAt);
            return value;
        }

        public AchievementView[] BuildViews()
        {
            if (!HasCatalog || !HasStates) return Array.Empty<AchievementView>();
            return _definitions.Select(d =>
            {
                _states.TryGetValue(d.Id, out var state);
                var complete = state.CompletedAt > 0;
                var definition = complete && state.HasRevealedDefinition && state.RevealedDefinition.Id == d.Id
                    ? state.RevealedDefinition
                    : d;
                var hidden = d.Hidden && !complete;
                return new AchievementView
                {
                    Id = d.Id, Name = definition.Name, Description = definition.Description, Category = d.Category,
                    IconPath = definition.IconPath, Target = hidden ? 0 : definition.Target,
                    Progress = hidden ? 0 : complete ? definition.Target : state.Progress,
                    CompletedAt = state.CompletedAt, ClaimedAt = state.ClaimedAt,
                    Hidden = d.Hidden,
                    Reward = hidden ? Reward.Empty : complete ? state.Reward : definition.Reward
                };
            }).ToArray();
        }
    }
}
