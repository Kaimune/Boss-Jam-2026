using UnityEngine;

namespace BossJam.Dialogue
{
    /// <summary>
    /// Maps a speaker token (the string used in the JSON file) to its visual
    /// and audio identity. One ScriptableObject asset per character.
    /// </summary>
    [CreateAssetMenu(menuName = "BossJam/Dialogue/Speaker Profile",
                     fileName = "NewSpeakerProfile")]
    public sealed class SpeakerProfile : ScriptableObject
    {
        [Tooltip("Exact token used as the 'speaker' field in dialogue JSON. " +
                 "Case-sensitive. Examples: \"theseus\", \"minotaur\".")]
        public string speakerToken;

        [Tooltip("Display name shown in the nameplate above the dialogue box.")]
        public string displayName;

        [Tooltip("Nameplate background colour.")]
        public Color nameplateColor = new Color(0.83f, 0.60f, 0.29f, 1f);

        [Tooltip("Render texture the dialogue UI swaps in when this speaker is active.")]
        public RenderTexture portraitTexture;

        [Tooltip("Tick SFX played for each non-whitespace character typed.")]
        public AudioClip letterTickClip;

        [Tooltip("Random pitch jitter applied to the tick clip per character.")]
        [Range(0f, 0.4f)] public float pitchJitter = 0.08f;
    }
}
