using UnityEngine;

namespace Game
{
    public enum SoundCue
    {
        YourTurn, Check, Bet, Call, Raise, AllIn, Fold, Win, Lose
    }

    public enum MusicCue
    {
        None, Lobby, Table, AllIn
    }

    [CreateAssetMenu(menuName = "Poker/Audio Library")]
    public sealed class GameAudioLibrary : ScriptableObject
    {
        [SerializeField] private AudioClip _yourTurn;
        [SerializeField] private AudioClip _check;
        [SerializeField] private AudioClip _bet;
        [SerializeField] private AudioClip _call;
        [SerializeField] private AudioClip _raise;
        [SerializeField] private AudioClip _allIn;
        [SerializeField] private AudioClip _fold;
        [SerializeField] private AudioClip _win;
        [SerializeField] private AudioClip _lose;
        [SerializeField] private AudioClip _lobbyMusic;
        [SerializeField] private AudioClip _tableMusic;
        [SerializeField] private AudioClip _allInMusic;

        public AudioClip GetSound(SoundCue cue) => cue switch
        {
            SoundCue.YourTurn => _yourTurn,
            SoundCue.Check => _check,
            SoundCue.Bet => _bet,
            SoundCue.Call => _call,
            SoundCue.Raise => _raise,
            SoundCue.AllIn => _allIn,
            SoundCue.Fold => _fold,
            SoundCue.Win => _win,
            SoundCue.Lose => _lose,
            _ => null,
        };

        public AudioClip GetMusic(MusicCue cue) => cue switch
        {
            MusicCue.Lobby => _lobbyMusic,
            MusicCue.Table => _tableMusic,
            MusicCue.AllIn => _allInMusic,
            _ => null,
        };
    }
}
