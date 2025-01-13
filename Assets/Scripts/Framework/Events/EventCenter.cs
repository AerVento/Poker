using System;
using System.Collections.Generic;

namespace Framework.Events
{
    /// <summary>
    /// The event center
    /// </summary>
    public static class EventCenter
    {
        private static Dictionary<string, Delegate> _events = new Dictionary<string, Delegate>();

        /// <summary>
        /// Add a listener to given event name.
        /// </summary>
        /// <param name="eventName">The event name.</param>
        /// <param name="listener">The listener.</param>
        public static void AddListener(string eventName, Delegate listener)
        {
            if (!_events.ContainsKey(eventName))
                _events.Add(eventName, listener);
            else
                _events[eventName] = Delegate.Combine(_events[eventName], listener);
        }

        /// <summary>
        /// Remove a listener from given event name.
        /// </summary>
        /// <param name="eventName">The event name.</param>
        /// <param name="listener">The listener.</param>
        public static void RemoveListener(string eventName, Delegate listener)
        {
            if (_events.ContainsKey(eventName))
            {
                _events[eventName] = Delegate.Remove(_events[eventName],listener);
            }
        }


        /// <summary>
        /// Trigger the all the listener of given event name with arguments.
        /// </summary>
        /// <param name="eventName">The given event name.</param>
        /// <param name="args">The arguments.</param>
        public static void Trigger(string eventName, params object[] args)
        {
            if (_events.ContainsKey(eventName))
            {
                _events[eventName]?.DynamicInvoke(args);
            }
        }
    }
}