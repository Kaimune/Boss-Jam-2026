using UnityEngine;


namespace BossJam.Configs
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "BossJam/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Min(0.01f)] public float tickDuration = 0.12f;
    }
}

