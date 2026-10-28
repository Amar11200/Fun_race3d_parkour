using UnityEngine;

namespace DashMasters
{
    public class CrusherGate : ObstacleBase
    {
        [Header("Crusher Settings")]
        [SerializeField] private float downY = 0.5f;
        [SerializeField] private float upY = 3.5f;
        [SerializeField] private float cycleDuration = 2.4f;
        [SerializeField] private float delay = 0f;

        private Vector3 basePos;
        private float lastHeight;
        private float verticalSpeed;

        protected override void Start()
        {
            base.Start();
            basePos = transform.localPosition;
            lastHeight = basePos.y;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            float t = ((Time.time + delay) % cycleDuration) / cycleDuration;
            float height;
            if (t < 0.25f)
            {
                float slamProgress = t / 0.25f;
                height = Mathf.Lerp(upY, downY, Mathf.Pow(slamProgress, 2f));
            }
            else if (t < 0.55f)
            {
                height = downY;
            }
            else
            {
                float riseProgress = (t - 0.55f) / 0.45f;
                height = Mathf.Lerp(downY, upY, Mathf.SmoothStep(0f, 1f, riseProgress));
            }

            verticalSpeed = (height - lastHeight) / Mathf.Max(Time.deltaTime, 0.001f);
            lastHeight = height;

            transform.localPosition = new Vector3(basePos.x, height, basePos.z);
        }

        public void Configure(float lowerY, float upperY, float duration, float offset)
        {
            downY = lowerY;
            upY = upperY;
            cycleDuration = duration;
            delay = offset;
            basePos = transform.localPosition;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            Vector3 back = -racerTransform.forward * 4.5f;
            Vector3 downPush = (verticalSpeed < -0.5f) ? Vector3.down * 1.5f : Vector3.up * 2.0f;
            return (back + downPush).normalized * pushbackForce;
        }
    }
}
