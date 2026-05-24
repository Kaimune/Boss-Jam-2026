using UnityEngine;
using UnityEngine.InputSystem;

public class BossDebugController : MonoBehaviour
{
    public Animator animator;

    public string slashAnimation = "slash_stepped";
    public string slamAnimation = "jump_stepped";

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            PlayAnimation(slashAnimation);

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            PlayAnimation(slamAnimation);
    }

    private void PlayAnimation(string animationName)
    {
        if (animator == null)
        {
            Debug.LogWarning("Animator missing");
            return;
        }

        animator.Play(animationName, 0, 0f);
        Debug.Log("Playing Animation: " + animationName);
    }
}