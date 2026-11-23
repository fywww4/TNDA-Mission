using UnityEngine;

namespace TNDA.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DemolitionGame))]
    public sealed class ArcadeMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip lobby;
        [SerializeField] private AudioClip stageOne;
        [SerializeField] private AudioClip stageTwo;
        [SerializeField] private AudioClip stageThree;
        [SerializeField] private AudioClip challenge;
        [SerializeField, Range(0, 1)] private float volume = .18f;

        private const float FadeSeconds = .35f;
        private ArcadeGame game;
        private AudioSource source;
        private float fade;
        private bool paused;

        public AudioClip TrackFor(ArcadeGame cabinet)
        {
            switch (cabinet.Phase)
            {
                case ArcadePhase.Attract:
                case ArcadePhase.Garage:
                    return lobby;
                case ArcadePhase.Story:
                case ArcadePhase.Countdown:
                case ArcadePhase.Playing:
                    switch (cabinet.ActiveStage?.Number)
                    {
                        case 1: return stageOne;
                        case 2: return stageTwo;
                        case 3: return stageThree;
                        case null: return cabinet.ActiveChallenge != null ? challenge : null;
                        default: return null;
                    }
                default:
                    return null;
            }
        }

        private void Awake()
        {
            game = GetComponent<DemolitionGame>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.volume = 0;
        }

        private void LateUpdate()
        {
            if (paused != game.CheatMenuOpen)
            {
                paused = game.CheatMenuOpen;
                if (paused) source.Pause();
                else source.UnPause();
            }
            if (paused) return;

            var track = TrackFor(game);
            bool changing = source.clip != track;
            fade = Mathf.MoveTowards(fade, changing || track == null ? 0 : 1,
                Time.unscaledDeltaTime / FadeSeconds);
            source.volume = volume * fade;

            // Finish fading out before starting the replacement on the same source.
            if (!changing || fade > 0) return;
            source.Stop();
            source.clip = track;
            if (track != null) source.Play();
        }

        private void OnDisable()
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            fade = 0;
            paused = false;
        }
    }
}
