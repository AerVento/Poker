using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// Normal Panel
    /// </summary>
    public interface IPanel 
    {
        /// <summary>
        /// The panel identifier which was set by UIManager to distinguish different panel instances.
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// If the panel was showing.
        /// </summary>
        public bool IsShowing { get; }

        /// <summary>
        /// Show the panel.
        /// </summary>
        public void Show();

        /// <summary>
        /// Hide the panel.
        /// </summary>
        public void Hide();
    }

    /// <summary>
    /// Panel which have fade in or fade out functions.
    /// </summary>
    public interface IAsyncPanel : IPanel
    {
        async void IPanel.Show()
        {
            await ShowAsync();
        }

        async void IPanel.Hide()
        {
            await HideAsync();
        }

        /// <summary>
        /// Show the panel asynchronizly.
        /// </summary>
        /// <returns>The awaiter.</returns>
        public UniTask ShowAsync();

        /// <summary>
        /// Hide the panel asynchronizly.
        /// </summary>
        /// <returns>The awaiter.</returns>
        public UniTask HideAsync();
    }
}

