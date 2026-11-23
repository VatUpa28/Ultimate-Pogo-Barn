using UnityEngine;

public class BarnJumpEffects : MonoBehaviour
{
    [SerializeField] private Material puffMaterial;
    private ParticleSystem puffs;

    private void Awake()
    {
        if (puffMaterial == null)
            return;

        var child = new GameObject("Pogo Takeoff Puffs");
        child.transform.SetParent(transform, false);
        puffs = child.AddComponent<ParticleSystem>();
        puffs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = puffs.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        main.gravityModifier = 0.3f;
        main.maxParticles = 100;

        var emission = puffs.emission;
        emission.enabled = false;
        var shape = puffs.shape;
        shape.enabled = false;

        var lifetimeColor = puffs.colorOverLifetime;
        lifetimeColor.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.65f, 0.32f), new GradientAlphaKey(0f, 1f) });
        lifetimeColor.color = new ParticleSystem.MinMaxGradient(fade);

        var size = puffs.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.65f, 1f, 1.25f));
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = puffMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    public void Emit(Vector3 position, Vector3 jumpDirection)
    {
        if (puffs == null)
            return;

        if (!puffs.isPlaying)
            puffs.Play();

        for (int i = 0; i < 12; i++)
        {
            bool sparkle = i >= 8;
            Vector2 offset = Random.insideUnitCircle;
            Vector3 spread = new Vector3(offset.x, 0f, offset.y);
            var particle = new ParticleSystem.EmitParams
            {
                position = position + spread * 0.17f,
                velocity = spread * (sparkle ? 3.25f : 1.85f) - jumpDirection * 0.65f
                           + Vector3.up * Random.Range(0.45f, sparkle ? 2.8f : 1.6f),
                startColor = sparkle ? new Color32(183, 243, 255, 255) : new Color32(255, 248, 218, 242),
                startSize = sparkle ? Random.Range(0.075f, 0.14f) : Random.Range(0.22f, 0.47f)
            };
            puffs.Emit(particle, 1);
        }
    }
}
