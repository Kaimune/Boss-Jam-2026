using UnityEngine;

public class BossSlashController : MonoBehaviour
{
    public ForwardSlashVFX slash;

    // Called by Animation Event
    public void PlaySlash()
    {
        slash.PlaySlash();
    }
}