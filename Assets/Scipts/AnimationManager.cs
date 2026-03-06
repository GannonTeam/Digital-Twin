using UnityEngine;

public class AnimationManager : MonoBehaviour
{
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();

        if (anim != null)
        {
            // 1. Pick a random frame between 1 and 120
            float randomFrame = Random.Range(10f, 120f);

            // 2. Convert to Normalized Time (Percentage of the clip)
            // We divide by 120 because that is your new total frame count
            float normalizedTime = randomFrame / 120f;

            // 3. Jump to that state immediately
            // Note: If your state name in the Animator is different, change "PlayAnimation" below
            anim.Play("PlayAnimation", 0, normalizedTime);

            Debug.Log($"{gameObject.name} started at frame: {randomFrame}");
        }
    }
}