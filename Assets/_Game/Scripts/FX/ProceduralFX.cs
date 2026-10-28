using UnityEngine;

namespace DashMasters
{
    public class ProceduralFX : MonoBehaviour
    {
        public static ProceduralFX Instance { get; private set; }

        private Material particleMat;
        private Material rippleMat;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Generate clean particle material
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            
            particleMat = new Material(shader);
            particleMat.color = Color.white;

            rippleMat = new Material(shader);
            rippleMat.color = new Color(0.85f, 0.98f, 1.0f, 0.65f);
        }

        public void SpawnCoinBurst(Vector3 position)
        {
            GameObject fxObj = new GameObject("CoinFX");
            fxObj.transform.position = position;

            ParticleSystem ps = fxObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer psRenderer = fxObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = particleMat;

            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.1f), new Color(1f, 1f, 0.6f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.0f);
            main.startLifetime = 0.5f;
            main.duration = 0.3f;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 16) });

            ps.Play();
        }

        public void SpawnHitPuff(Vector3 position)
        {
            GameObject fxObj = new GameObject("HitFX");
            fxObj.transform.position = position;

            ParticleSystem ps = fxObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer psRenderer = fxObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = particleMat;

            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.2f), new Color(1f, 0.7f, 0.2f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startLifetime = 0.4f;
            main.duration = 0.2f;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 20) });

            ps.Play();
        }

        public void SpawnConfetti(Vector3 position)
        {
            GameObject fxObj = new GameObject("ConfettiFX");
            fxObj.transform.position = position;

            ParticleSystem ps = fxObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer psRenderer = fxObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = particleMat;

            var main = ps.main;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.cyan, 0.0f), 
                    new GradientColorKey(Color.magenta, 0.3f), 
                    new GradientColorKey(Color.yellow, 0.6f),
                    new GradientColorKey(Color.green, 1.0f) 
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(grad);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startLifetime = 1.8f;
            main.gravityModifier = 0.7f;
            main.duration = 1.0f;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 1.2f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { 
                new ParticleSystem.Burst(0.0f, 60),
                new ParticleSystem.Burst(0.2f, 40)
            });

            ps.Play();
        }

        public void SpawnWaterSplash(Vector3 position)
        {
            GameObject fxObj = new GameObject("WaterSplashFX");
            fxObj.transform.position = position;

            // 1. Upward splash droplets & white foam fountain
            ParticleSystem ps = fxObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer psRenderer = fxObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = particleMat;

            var main = ps.main;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.25f, 0.92f, 1.0f), 0.0f), // Vibrant Turquoise
                    new GradientColorKey(Color.white, 0.35f),                  // Crisp White Foam
                    new GradientColorKey(new Color(0.08f, 0.65f, 0.95f), 1.0f) // Tropical Blue
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(grad);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 9.5f);
            main.startLifetime = 0.85f;
            main.gravityModifier = 1.35f;
            main.duration = 0.45f;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.65f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // Point upwards

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 40)
            });

            ps.Play();

            // 2. Expanding horizontal water ripple ring on water surface
            SpawnSurfaceRippleRing(position);
        }

        private void SpawnSurfaceRippleRing(Vector3 position)
        {
            GameObject rippleObj = new GameObject("WaterRippleRing");
            rippleObj.transform.position = new Vector3(position.x, -5.45f, position.z);

            ParticleSystem ps = rippleObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer psRenderer = rippleObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = rippleMat;

            var main = ps.main;
            main.startColor = new Color(0.7f, 0.95f, 1.0f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 4.5f);
            main.startLifetime = 1.1f;
            main.duration = 0.25f;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(90f, 0f, 0f); // Flat horizontal circle on water surface

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 3.8f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.4f, 0.85f, 1.0f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGrad);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 28)
            });

            ps.Play();
        }
    }
}
