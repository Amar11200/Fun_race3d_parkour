using System.Collections.Generic;
using UnityEngine;

namespace DashMasters
{
    /// <summary>
    /// Large 3D Segmented Circular Ring Obstacle.
    /// Dominates the course with alternating vibrant pink and dark magenta curved segments,
    /// golden joint collars, and a blue circular floor/platform detail directly underneath/through the ring.
    /// Supports controlled revolving or pendulum swinging motion with realistic physical tangential knockback.
    /// </summary>
    public class LargePinkSegmentedRing : ObstacleBase
    {
        public enum RingMotionMode
        {
            Revolving,
            PendulumSwing
        }

        [Header("Motion Configuration")]
        [SerializeField] private RingMotionMode motionMode = RingMotionMode.Revolving;
        [SerializeField] private float rotationSpeed = 48f;
        [SerializeField] private bool rotateClockwise = true;
        [SerializeField] private float maxSwingAngle = 44f;
        [SerializeField] private float swingFrequency = 1.85f;
        [SerializeField] private Transform movingRingTransform;

        private Rigidbody cachedRigidbody;
        private float currentAngle;

        protected override void Start()
        {
            base.Start();
            cachedRigidbody = GetComponent<Rigidbody>();
            if (cachedRigidbody == null)
            {
                cachedRigidbody = gameObject.AddComponent<Rigidbody>();
            }
            cachedRigidbody.isKinematic = true;
            cachedRigidbody.useGravity = false;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            if (movingRingTransform == null) return;

            if (motionMode == RingMotionMode.Revolving)
            {
                float dir = rotateClockwise ? 1f : -1f;
                float delta = dir * rotationSpeed * Time.deltaTime;
                currentAngle += delta;
                movingRingTransform.Rotate(Vector3.up, delta, Space.Self);
            }
            else
            {
                // Smooth sinusoidal horizontal pendulum swing around vertical axis
                float swingAngle = maxSwingAngle * Mathf.Sin(Time.time * swingFrequency);
                movingRingTransform.localRotation = Quaternion.Euler(0f, swingAngle, 0f);
            }
        }

        public void Configure(Transform ringTransform, RingMotionMode mode, float speed, bool clockwise, float maxSwing = 44f, float swingFreq = 1.85f)
        {
            movingRingTransform = ringTransform;
            motionMode = mode;
            rotationSpeed = speed;
            rotateClockwise = clockwise;
            maxSwingAngle = maxSwing;
            swingFrequency = swingFreq;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            Vector3 center = (movingRingTransform != null) ? movingRingTransform.position : transform.position;
            Vector3 radiusVec = contactPoint - center;
            radiusVec.y = 0f;

            Vector3 tangentDir;
            if (motionMode == RingMotionMode.Revolving)
            {
                tangentDir = Vector3.Cross(Vector3.up, radiusVec.normalized) * (rotateClockwise ? 1f : -1f);
            }
            else
            {
                float swingVelSign = Mathf.Cos(Time.time * swingFrequency);
                tangentDir = Vector3.Cross(Vector3.up, radiusVec.normalized) * (swingVelSign >= 0f ? 1f : -1f);
            }

            Vector3 outward = radiusVec.normalized;

            // Direct forceful fling off the circular platform into the water
            Vector3 knockbackDir = (tangentDir * 0.85f + outward * 0.45f - racerTransform.forward * 0.20f + Vector3.up * 0.48f).normalized;
            return knockbackDir * (pushbackForce * 1.45f);
        }
    }
}
