using UnityEngine;

namespace DashMasters
{
    public class PinkWhiteCurvedObstacle : ObstacleBase
    {
        [Header("Curved Ring Settings")]
        [SerializeField] private float rotationSpeed = 85f;
        [SerializeField] private Vector3 rotationAxis = Vector3.forward;
        [SerializeField] private Transform ringPivot;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            Transform target = ringPivot != null ? ringPivot : transform;
            target.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
        }

        public void Configure(Transform pivot, float speed, Vector3 axis)
        {
            ringPivot = pivot;
            rotationSpeed = speed;
            rotationAxis = axis;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            Vector3 backAlongTrack = -racerTransform.forward * 4.5f;
            Vector3 radial = (racerTransform.position - transform.position);
            radial.y = 0f;
            Vector3 deflect = radial.sqrMagnitude > 0.01f ? radial.normalized * 3.5f : Vector3.zero;

            return (backAlongTrack + deflect + Vector3.up * 3.5f).normalized * pushbackForce;
        }
    }
}
