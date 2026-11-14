
using System.Collections;
using UnityEngine;

public class MenuAnimation : MonoBehaviour
{
    [System.Serializable]
    public class AnimatedButton
    {
        public RectTransform button;
        public CanvasGroup canvasGroup;
    }

    public AnimatedButton[] buttons;

    public float duration = 0.7f;
    public float delayBetweenButtons = 0.15f;
    public float swingAngle = 15f;
    public float idleSwingAngle = 3f;
    public float idleSwingSpeed = 2f;

    [Header("Title Animation")]
    public CanvasGroup titleCanvasGroup;
    public float titleFadeDuration = 1.2f;

    private void Start()
    {
        StartCoroutine(AnimateTitle());
        StartCoroutine(AnimateButtons());
        StartCoroutine(IdleSwing());
    }

    private IEnumerator AnimateTitle()
    {
        titleCanvasGroup.alpha = 0;

        float elapsed = 0f;

        while (elapsed < titleFadeDuration)
        {
            elapsed += Time.deltaTime;

            titleCanvasGroup.alpha =
                Mathf.Clamp01(elapsed / titleFadeDuration);

            yield return null;
        }

        titleCanvasGroup.alpha = 1;
    }

    private IEnumerator AnimateButtons()
    {
        // Set all buttons to their starting state
        foreach (AnimatedButton button in buttons)
        {
            button.canvasGroup.alpha = 0;
            button.button.localRotation =
                Quaternion.Euler(0, 0, swingAngle);
        }

        // Animate all buttons simultaneously
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            foreach (AnimatedButton button in buttons)
            {
                button.canvasGroup.alpha = t;

                float angle = swingAngle *
                    (1 - t) * Mathf.Cos(t * Mathf.PI * 2);

                button.button.localRotation =
                    Quaternion.Euler(0, 0, angle);
            }

            yield return null;
        }

        // Ensure final state
        foreach (AnimatedButton button in buttons)
        {
            button.canvasGroup.alpha = 1;
            button.button.localRotation = Quaternion.identity;
        }
    }

    private IEnumerator IdleSwing()
    {
        while (true)
        {
            float time = Time.time;

            foreach (AnimatedButton button in buttons)
            {
                float angle = Mathf.Sin(time * idleSwingSpeed) 
                            * idleSwingAngle;

                button.button.localRotation =
                    Quaternion.Euler(0, 0, angle);
            }

            yield return null;
        }
    }
}
