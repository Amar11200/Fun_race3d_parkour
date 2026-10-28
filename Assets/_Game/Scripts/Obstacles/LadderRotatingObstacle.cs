using UnityEngine;

namespace DashMasters
{
    /// <summary>
    /// Rotating obstacle mounted on the vertical climbing ladder (matching reference image).
    /// Features an axle rod with yellow spherical joints, a red drum with green rectangular studs,
    /// and a sweeper arm that sweeps across the ladder rungs as it rotates.
    /// Climbers must pause and time their climb to pass when the arm clears the ladder.
    /// </summary>
    public class LadderRotatingObstacle : ObstacleBase
    {
        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 65f;
        [SerializeField] private bool rotateClockwise = true;
        [SerializeField] private Transform rotatingHub;

        [Header("Ladder Timing & Safety")]
        [SerializeField] private float safeAngleStart = 45f;
        [SerializeField] private float safeAngleEnd = 315f;

        private float currentAngle = 0f;

        public float RotationSpeed { get => rotationSpeed; set => rotationSpeed = value; }
        public bool RotateClockwise { get => rotateClockwise; set => rotateClockwise = value; }
        public Transform RotatingHub { get => rotatingHub; set => rotatingHub = value; }

        protected override void Start()
        {
            base.Start();
            pushbackForce = 7.5f;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private void Update()
        {
            float dir = rotateClockwise ? 1f : -1f;
            currentAngle = (currentAngle + dir * rotationSpeed * Time.deltaTime) % 360f;
            if (currentAngle < 0f) currentAngle += 360f;

            if (rotatingHub != null)
            {
                // Rotates around local X axis (horizontal axle along ladder face)
                rotatingHub.localRotation = Quaternion.Euler(currentAngle, 0f, 0f);
            }
        }

        /// <summary>
        /// Returns true if the rotating arm is currently swung away from the ladder plane,
        /// leaving the climbing rungs safe to traverse.
        /// </summary>
        public bool IsSafeToPass()
        {
            float normAngle = currentAngle % 360f;
            // The danger window is when the arm is sweeping through the ladder plane (near 0° / 360°)
            // Safe window is when the arm is pointed away / outward (between safeAngleStart and safeAngleEnd)
            return normAngle >= safeAngleStart && normAngle <= safeAngleEnd;
        }

        /// <summary>
        /// Angle difference from the danger crossing position (0 degrees)
        /// </summary>
        public float AngleToDanger()
        {
            float normAngle = currentAngle % 360f;
            if (normAngle > 180f) normAngle = 360f - normAngle;
            return normAngle;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            // Fling the climber backward off the vertical ladder into open air over water
            Vector3 pushDir = -transform.forward; // Outward away from ladder wall
            pushDir.y = -0.35f; // Slight downward tumble
            return (pushDir.normalized * 0.85f + Vector3.up * 0.25f).normalized * pushbackForce;
        }
    }
}
