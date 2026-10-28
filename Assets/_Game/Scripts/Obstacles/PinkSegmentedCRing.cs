using UnityEngine;

namespace DashMasters
{
    /// <summary>
    /// Pink Segmented C-Shaped Obstacle.
    /// Features a thick, rounded, 3D tube-like arc with alternating vibrant pink and darker magenta segments,
    /// spherical golden joints, rounded endcaps, and a deliberate opening on one side forming a distinct C-shape.
    /// Rotates smoothly around the vertical axis directly over the white racing track with open blue water visible around it.
    /// </summary>
    public class PinkSegmentedCRing : ObstacleBase
    {
        [Header("C-Ring Rotation Settings")]
        [SerializeField] private float rotationSpeed = 42f;
        [SerializeField] private bool rotateClockwise = true;
        [SerializeField] private Transform movingRingTransform;

        private float currentAngle;

        public float RotationSpeed => rotationSpeed;
        public bool RotateClockwise => rotateClockwise;

        protected override void Start()
        {
            base.Start();
            if (movingRingTransform == null)
            {
                Transform t = transform.Find("RotatingRing");
                if (t != null) movingRingTransform = t;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            if (movingRingTransform == null) return;

            float dir = rotateClockwise ? 1f : -1f;
            float delta = dir * rotationSpeed * Time.deltaTime;
            currentAngle += delta;

            // Controlled horizontal rotation around vertical axis
            movingRingTransform.Rotate(Vector3.up, delta, Space.Self);
        }

        public void Configure(Transform ringTransform, float speed, bool clockwise)
        {
            movingRingTransform = ringTransform;
            rotationSpeed = speed;
            rotateClockwise = clockwise;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            Vector3 center = (movingRingTransform != null) ? movingRingTransform.position : transform.position;
            Vector3 radiusVec = contactPoint - center;
            radiusVec.y = 0f; // local horizontal plane
            if (radiusVec.sqrMagnitude < 0.01f)
            {
                radiusVec = racerTransform.position - center;
                radiusVec.y = 0f;
                if (radiusVec.sqrMagnitude < 0.01f) radiusVec = -racerTransform.forward;
            }

            Vector3 tangentDir = Vector3.Cross(Vector3.up, radiusVec.normalized) * (rotateClockwise ? 1f : -1f);
            Vector3 outward = radiusVec.normalized;

            // Tangential sweep flings racers outward into the surrounding water
            Vector3 knockbackDir = (tangentDir * 0.85f + outward * 0.45f - racerTransform.forward * 0.20f + Vector3.up * 0.45f).normalized;
            return knockbackDir * (pushbackForce * 1.35f);
        }
    }
}
