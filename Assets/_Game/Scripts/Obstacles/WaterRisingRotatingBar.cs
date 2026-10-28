using UnityEngine;

namespace DashMasters
{
    /// <summary>
    /// Rotating Bar Obstacle emerging from deep underwater beneath the racing track.
    /// Features a heavy industrial pylon rooted below the water line, a vibrant cyan/blue hub
    /// with golden/orange collar details, and one or two long red rotating bars sweeping horizontally
    /// across the walkable ribbon road.
    /// </summary>
    public class WaterRisingRotatingBar : ObstacleBase
    {
        [Header("Water-Rising Bar Configuration")]
        [SerializeField] private float rotationSpeed = 85f;
        [SerializeField] private bool rotateClockwise = true;
        [SerializeField] private float barLength = 3.8f;
        [SerializeField] private Transform rotatingPivot;

        private float currentAngle;

        public float RotationSpeed => rotationSpeed;
        public bool RotateClockwise => rotateClockwise;
        public float BarLength => barLength;

        protected override void Start()
        {
            base.Start();
            if (rotatingPivot == null)
            {
                Transform p = transform.Find("Pivot");
                if (p != null) rotatingPivot = p;
                else rotatingPivot = transform;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            float dir = rotateClockwise ? 1f : -1f;
            float delta = dir * rotationSpeed * Time.deltaTime;
            currentAngle += delta;

            if (rotatingPivot != null)
            {
                rotatingPivot.Rotate(Vector3.up, delta, Space.Self);
            }
        }

        public void Configure(Transform pivotTransform, float speed, bool clockwise, float length = 3.8f)
        {
            rotatingPivot = pivotTransform;
            rotationSpeed = speed;
            rotateClockwise = clockwise;
            barLength = length;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            Vector3 center = (rotatingPivot != null) ? rotatingPivot.position : transform.position;
            Vector3 radiusVec = contactPoint - center;
            radiusVec.y = 0f;

            // Tangential linear velocity: v = omega x r
            Vector3 worldAxis = Vector3.up * (rotateClockwise ? 1f : -1f);
            Vector3 tangVel = Vector3.Cross(worldAxis, radiusVec.normalized);

            // If contact is near hub, push radially outwards and off the road
            if (radiusVec.sqrMagnitude < 0.8f)
            {
                Vector3 radial = (racerTransform.position - center);
                radial.y = 0f;
                tangVel = (radial == Vector3.zero ? -racerTransform.forward : radial.normalized);
            }

            // High-energy arcade fling: outward into the blue water + backward track impulse + vertical lift
            Vector3 finalDir = (tangVel.normalized * 0.85f - racerTransform.forward * 0.35f + Vector3.up * 0.45f).normalized;
            return finalDir * (pushbackForce * 1.35f);
        }
    }
}
