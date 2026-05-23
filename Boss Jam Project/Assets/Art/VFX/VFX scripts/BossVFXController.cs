using UnityEngine;

public class BossVFXController : MonoBehaviour
{
    public ForwardSlashVFX forwardSlashVFX;
    public SlamVFX slamVFX;
    public SlamEchoVFX slamEchoVFX;

    public void PlayForwardSlash()
    {
        if (forwardSlashVFX != null)
            forwardSlashVFX.PlaySlash();
    }

    public void PlaySlam()
    {
        if (slamVFX != null)
            slamVFX.PlaySlam();

        if (slamEchoVFX != null)
            slamEchoVFX.PlayEcho();
    }
}