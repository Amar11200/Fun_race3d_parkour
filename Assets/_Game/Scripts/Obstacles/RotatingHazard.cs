using UnityEngine;

namespace DashMasters
{
    public class RotatingHazard : ObstacleBase
    {
        [Header("Rotation Settings")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float rotationSpeed = 120f;

        public Vector3 RotationAxis => rotationAxis;
        public float RotationSpeed => rotationSpeed;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
        }

        public void Configure(Vector3 axis, float speed)
        {
            rotationAxis = axis;
            rotationSpeed = speed;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            // Calculate rotational angular velocity in world coordinates
            Vector3 worldAxis = transform.TransformDirection(rotationAxis.normalized);
            Vector3 angVel = worldAxis * (rotationSpeed * Mathf.Deg2Rad);

            // Radius vector from pivot center to impact point
            Vector3 r = contactPoint - transform.position;
            r.y = 0f;

            // Tangential linear velocity: v = w x r
            Vector3 tangVel = Vector3.Cross(angVel, r);

            // If contact is close to axle, push radially outwards and back
            if (tangVel.sqrMagnitude < 0.5f)
            {
                Vector3 radial = (racerTransform.position - transform.position);
                radial.y = 0f;
                tangVel = (radial == Vector3.zero ? -racerTransform.forward : radial.normalized) * (Mathf.Abs(rotationSpeed) * 0.05f);
            }

            // Combine sweep tangent, backward track impulse, and vertical lift
            Vector3 finalDir = (tangVel.normalized * 0.85f - racerTransform.forward * 0.35f + Vector3.up * 0.45f).normalized;
            return finalDir * pushbackForce;
        }
    }
}
