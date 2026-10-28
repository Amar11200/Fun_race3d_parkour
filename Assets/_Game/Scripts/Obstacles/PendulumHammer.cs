using UnityEngine;

namespace DashMasters
{
    public class PendulumHammer : ObstacleBase
    {
        [Header("Swing Configuration")]
        [SerializeField] private Transform pivot;
        [SerializeField] private float maxSwingAngle = 65f;
        [SerializeField] private float swingSpeed = 2.4f;
        [SerializeField] private float phaseOffset = 0f;

        private float currentAngle;
        private float angularVelocity;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            if (pivot != null)
            {
                float t = (Time.time * swingSpeed) + phaseOffset;
                currentAngle = Mathf.Sin(t) * maxSwingAngle;
                // Derivative: instantaneous swing angular velocity
                angularVelocity = Mathf.Cos(t) * swingSpeed * maxSwingAngle;

                pivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
            }
        }

        public void Configure(Transform pivotTransform, float maxAngle, float speed, float phase)
        {
            pivot = pivotTransform;
            maxSwingAngle = maxAngle;
            swingSpeed = speed;
            phaseOffset = phase;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            // Tangential swing direction in world space
            Vector3 swingAxis = transform.right * (angularVelocity >= 0 ? 1f : -1f);
            float speedRatio = Mathf.Clamp(Mathf.Abs(angularVelocity) / (swingSpeed * maxSwingAngle), 0.35f, 1.0f);

            Vector3 impactDir = (swingAxis * 0.85f - racerTransform.forward * 0.3f + Vector3.up * 0.45f).normalized;
            return impactDir * (pushbackForce * speedRatio * 1.35f);
        }
    }
}
