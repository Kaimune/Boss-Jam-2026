using UnityEngine;

public class BossVFXController : MonoBehaviour
{
    public ForwardSlashVFX forwardSlashVFX;
    public SlamVFX slamVFX;
    public SlamEchoVFX slamEchoVFX;

    public AxeSlashTrail axeSlashTrail;

    public void PlayForwardSlashVFX()
    {
        if (forwardSlashVFX != null)
            forwardSlashVFX.PlaySlash();
    }

    public void PlaySlamVFX()
    {
        if (slamVFX != null)
            slamVFX.PlaySlam();

        if (slamEchoVFX != null)
            slamEchoVFX.PlayEcho();
    }

    public void StartAxeTrail()
    {
        if (axeSlashTrail != null)
            axeSlashTrail.StartTrail();
    }

    public void StopAxeTrail()
    {
        if (axeSlashTrail != null)
            axeSlashTrail.StopTrail();
    }
}