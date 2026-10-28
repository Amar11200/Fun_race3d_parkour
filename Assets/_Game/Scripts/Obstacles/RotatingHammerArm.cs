using UnityEngine;

namespace DashMasters
{
    public class RotatingHammerArm : ObstacleBase
    {
        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 65f;
        [SerializeField] private bool clockwise = true;
        [SerializeField] private float armRadius = 2.6f;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            float dir = clockwise ? 1f : -1f;
            transform.Rotate(Vector3.up, dir * rotationSpeed * Time.deltaTime, Space.Self);
        }

        public void Configure(float speed, bool isClockwise, float radius)
        {
            rotationSpeed = speed;
            clockwise = isClockwise;
            armRadius = radius;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            // Tangential speed: v = omega * r
            Vector3 center = transform.position;
            Vector3 radiusVec = contactPoint - center;
            radiusVec.y = 0f;

            Vector3 tangent = Vector3.Cross(Vector3.up, radiusVec.normalized) * (clockwise ? 1f : -1f);
            Vector3 knockDir = (tangent * 0.85f - racerTransform.forward * 0.35f + Vector3.up * 0.45f).normalized;

            return knockDir * (pushbackForce * 1.35f);
        }
    }
}
