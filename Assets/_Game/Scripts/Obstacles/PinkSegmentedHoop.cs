using System.Collections.Generic;
using UnityEngine;

namespace DashMasters
{
    /// <summary>
    /// Pink Segmented Hoop Obstacle lying horizontally flat on the track.
    /// Rotates around the vertical Y-axis like a spinning horizontal ring.
    /// </summary>
    public class PinkSegmentedHoop : ObstacleBase
    {
        [Header("Hoop Rotation")]
        [SerializeField] private float rotationSpeed = 45f;
        [SerializeField] private bool rotateClockwise = true;
        [SerializeField] private Transform rotatingRing;

        private float currentAngle;

        public float RotationSpeed => rotationSpeed;
        public bool RotateClockwise => rotateClockwise;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            float dir = rotateClockwise ? 1f : -1f;
            float delta = dir * rotationSpeed * Time.deltaTime;
            currentAngle += delta;

            if (rotatingRing != null)
            {
                // Rotates horizontally around the vertical axis
                rotatingRing.Rotate(Vector3.up, delta, Space.Self);
            }
        }

        public void Configure(Transform ringTransform, float speed, bool clockwise)
        {
            rotatingRing = ringTransform;
            rotationSpeed = speed;
            rotateClockwise = clockwise;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            // Calculate tangential horizontal velocity of the rotating hoop segment: v = omega x r
            Vector3 center = (rotatingRing != null) ? rotatingRing.position : transform.position;
            Vector3 radiusVec = contactPoint - center;
            radiusVec.y = 0f; // horizontal plane

            Vector3 tangentDir = Vector3.Cross(Vector3.up, radiusVec.normalized) * (rotateClockwise ? 1f : -1f);
            Vector3 outward = radiusVec.normalized;
            Vector3 knockbackDir = (tangentDir * 0.80f + outward * 0.45f - racerTransform.forward * 0.20f + Vector3.up * 0.45f).normalized;

            return knockbackDir * (pushbackForce * 1.25f);
        }
    }
}
