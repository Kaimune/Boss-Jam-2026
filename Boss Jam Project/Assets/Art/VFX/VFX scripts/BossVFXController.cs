using UnityEngine;

public class BossVFXController : MonoBehaviour
{
    public ForwardSlashVFX forwardSlashVFX;
    public SlamVFX slamVFX;
    public SlamEchoVFX slamEchoVFX;
public void PlayForwardSlashVFX()
{
    Debug.Log("Forward slash VFX event fired");

    if (forwardSlashVFX != null)
        forwardSlashVFX.PlaySlash();
    else
        Debug.LogWarning("ForwardSlashVFX reference missing");
}

public void PlaySlamVFX()
{
    Debug.Log("SLAM VFX EVENT FIRED");

    if (slamVFX != null)
        slamVFX.PlaySlam();
    else
        Debug.LogWarning("slamVFX is missing");

    if (slamEchoVFX != null)
        slamEchoVFX.PlayEcho();
    else
        Debug.LogWarning("slamEchoVFX is missing");
}
}