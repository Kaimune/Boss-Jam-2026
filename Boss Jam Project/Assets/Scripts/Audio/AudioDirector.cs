using System.Collections;
using BossJam.Game;
using UnityEngine;

namespace BossJam.Audio
{
    /// <summary>
    /// Scene-scoped audio brain. Owns two AudioSources — one looped BGM, one
    /// PlayOneShot bus for SFX — and crossfades the BGM in response to
    /// <see cref="GameStateController"/> transitions. Boss/UI UnityEvents
    /// target <see cref="PlayOneShot"/> to fire one-off clips.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [Header("Sources (auto-added if left null)")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("BGM by state (null = stop music)")]
        [SerializeField] private AudioClip startupBgm;
        [SerializeField] private AudioClip playingBgm;
        [SerializeField] private AudioClip deathBgm;
        [SerializeField] private AudioClip gameOverBgm;

        [Header("Crossfade")]
        [Tooltip("Seconds for BGM fade out + fade in on a state change. Uses unscaled time so it works while paused.")]
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.6f;

        private GameStateController state;
        private float targetMusicVolume = 1f;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource   == null) sfxSource   = gameObject.AddComponent<AudioSource>();

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            targetMusicVolume = musicSource.volume;

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        private void OnEnable()
        {
            state = GameStateController.Instance != null
                ? GameStateController.Instance
                : FindFirstObjectByType<GameStateController>();

            if (state != null)
            {
                state.StateChanged += HandleStateChanged;
                HandleStateChanged(state.State);   // sync to current state immediately
            }
            else
            {
                // No state controller in scene — just play whatever BGM is set for Playing.
                SwitchTo(playingBgm);
            }
        }

        private void OnDisable()
        {
            if (state != null) state.StateChanged -= HandleStateChanged;
            if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Fire-and-forget SFX. Safe to wire from a UnityEvent.</summary>
        public void PlayOneShot(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip);
        }

        /// <summary>Fire-and-forget SFX with a per-call volume scale (0..1+).</summary>
        public void PlayOneShot(AudioClip clip, float volumeScale)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, volumeScale);
        }

        /// <summary>Static convenience for code-driven calls; no-ops if no AudioDirector exists.</summary>
        public static void Sfx(AudioClip clip) => Instance?.PlayOneShot(clip);

        /// <summary>Static convenience with per-call volume scale.</summary>
        public static void Sfx(AudioClip clip, float volumeScale) => Instance?.PlayOneShot(clip, volumeScale);

        private void HandleStateChanged(GameState s) => SwitchTo(ClipForState(s));

        private AudioClip ClipForState(GameState s)
        {
            switch (s)
            {
                case GameState.Startup:  return startupBgm;
                case GameState.Playing:  return playingBgm;
                case GameState.Death:    return deathBgm;
                case GameState.GameOver: return gameOverBgm;
                default: return null;
            }
        }

        private void SwitchTo(AudioClip next)
        {
            if (musicSource == null) return;
            if (musicSource.clip == next && (next == null || musicSource.isPlaying)) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(CrossfadeTo(next));
        }

        private IEnumerator CrossfadeTo(AudioClip next)
        {
            float fade = Mathf.Max(0.001f, crossfadeSeconds);

            // Fade out current track (if any).
            if (musicSource.isPlaying)
            {
                float startVol = musicSource.volume;
                float t = 0f;
                while (t < fade)
                {
                    t += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(startVol, 0f, t / fade);
                    yield return null;
                }
                musicSource.Stop();
            }

            if (next == null)
            {
                musicSource.clip = null;
                musicSource.volume = targetMusicVolume;
                fadeRoutine = null;
                yield break;
            }

            // Fade in new track.
            musicSource.clip = next;
            musicSource.loop = true;
            musicSource.volume = 0f;
            musicSource.Play();

            float t2 = 0f;
            while (t2 < fade)
            {
                t2 += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(0f, targetMusicVolume, t2 / fade);
                yield return null;
            }
            musicSource.volume = targetMusicVolume;
            fadeRoutine = null;
        }
    }
}
