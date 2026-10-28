using UnityEngine;

namespace DashMasters
{
    public class GreenPlatformHazard : ObstacleBase
    {
        [Header("Hazard Rod Settings")]
        [SerializeField] private Transform spinningRod;
        [SerializeField] private float rotationSpeed = 160f;
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            if (spinningRod != null)
            {
                spinningRod.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
            }
            else
            {
                transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
            }
        }

        public void Configure(Transform rod, float speed, Vector3 axis)
        {
            spinningRod = rod;
            rotationSpeed = speed;
            rotationAxis = axis;
        }

        public override Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            // Low tripping knockback: send racer tumbling backward and off the side
            Vector3 back = -racerTransform.forward * 4.2f;
            Vector3 side = (contactPoint.x > transform.position.x) ? transform.right * 3.5f : -transform.right * 3.5f;
            return (back + side + Vector3.up * 4.2f).normalized * pushbackForce;
        }
    }
}
