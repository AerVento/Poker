using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Game.Events;

namespace Game.Achievements
{
    public interface IAchievementHandler<in TEvent> where TEvent : PlayerEvent
    {
        void Evaluate(TEvent playerEvent, string accountId, AchievementProgress progress);
    }

    public abstract class HandAchievement : Achievement, IAchievementHandler<HandStageRecordedEvent>
    {
        public abstract override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress);
        public void Evaluate(HandStageRecordedEvent playerEvent, string accountId, AchievementProgress progress)
            => Evaluate(playerEvent.Context, accountId, progress);
    }

    public sealed class AchievementEventBinding
    {
        public Achievement Definition { get; }
        public Action<PlayerEvent, string, AchievementProgress> Evaluate { get; }

        public AchievementEventBinding(Achievement definition, Action<PlayerEvent, string, AchievementProgress> evaluate)
        {
            Definition = definition;
            Evaluate = evaluate;
        }
    }

    public sealed class AchievementEventRouter
    {
        private readonly Dictionary<Type, List<AchievementEventBinding>> _bindings =
            new Dictionary<Type, List<AchievementEventBinding>>();

        public AchievementEventRouter(AchievementCatalog catalog)
        {
            foreach (var definition in catalog.All)
            {
                var handlers = definition.GetType().GetInterfaces().Where(t => t.IsGenericType &&
                    t.GetGenericTypeDefinition() == typeof(IAchievementHandler<>)).ToArray();
                foreach (var handler in handlers)
                {
                    var method = handler.GetMethod("Evaluate");
                    Add(handler.GetGenericArguments()[0], new AchievementEventBinding(definition, (value, account, progress) =>
                    {
                        try { method.Invoke(definition, new object[] { value, account, progress }); }
                        catch (TargetInvocationException exception) when (exception.InnerException != null)
                        {
                            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                            throw;
                        }
                    }));
                }
                // Keep source compatibility with existing extensions overriding the old virtual hook.
                if (!handlers.Any(t => t.GetGenericArguments()[0] == typeof(HandStageRecordedEvent)) &&
                    definition.GetType().GetMethod("Evaluate", new[] { typeof(AchievementContext), typeof(string), typeof(AchievementProgress) })
                        ?.DeclaringType != typeof(Achievement))
                {
                    Add(typeof(HandStageRecordedEvent), new AchievementEventBinding(definition, (value, account, progress) =>
                        definition.Evaluate(((HandStageRecordedEvent)value).Context, account, progress)));
                }
            }
        }

        private void Add(Type type, AchievementEventBinding binding)
        {
            if (!_bindings.TryGetValue(type, out var entries)) _bindings.Add(type, entries = new List<AchievementEventBinding>());
            entries.Add(binding);
        }

        public IEnumerable<AchievementEventBinding> For(PlayerEvent playerEvent)
            => _bindings.TryGetValue(playerEvent.GetType(), out var entries) ? entries : Enumerable.Empty<AchievementEventBinding>();
    }
}
