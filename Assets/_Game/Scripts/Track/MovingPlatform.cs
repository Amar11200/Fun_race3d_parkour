using UnityEngine;

namespace DashMasters
{
    public enum MovingPlatformType
    {
        Horizontal,
        Vertical,
        Rotating,
        SteppingGap
    }

    public class MovingPlatform : MonoBehaviour
    {
        [Header("Platform Type")]
        [SerializeField] private MovingPlatformType platformType = MovingPlatformType.Horizontal;

        [Header("Motion Settings")]
        [SerializeField] private Vector3 localMoveAxis = Vector3.right;
        [SerializeField] private float travelDistance = 2.4f;
        [SerializeField] private float moveSpeed = 2.0f;
        [SerializeField] private float phaseOffset = 0f;
        [SerializeField] private float rotationSpeed = 35f;

        private Vector3 startLocalPos;
        private Quaternion startLocalRot;
        private Vector3 prevWorldPos;
        private Quaternion prevWorldRot;

        public Vector3 Velocity { get; private set; }
        public float AngularVelocityY { get; private set; }

        private void Start()
        {
            startLocalPos = transform.localPosition;
            startLocalRot = transform.localRotation;
            prevWorldPos = transform.position;
            prevWorldRot = transform.rotation;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                Velocity = Vector3.zero;
                AngularVelocityY = 0f;
                return;
            }

            float t = (Time.time * moveSpeed) + phaseOffset;

            switch (platformType)
            {
                case MovingPlatformType.Horizontal:
                {
                    float offset = Mathf.Sin(t) * travelDistance;
                    Vector3 axis = localMoveAxis.normalized;
                    transform.localPosition = startLocalPos + (axis * offset);
                    break;
                }
                case MovingPlatformType.Vertical:
                {
                    float offset = (Mathf.Sin(t) * 0.5f + 0.5f) * travelDistance;
                    transform.localPosition = startLocalPos + Vector3.up * offset;
                    break;
                }
                case MovingPlatformType.Rotating:
                {
                    transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
                    break;
                }
                case MovingPlatformType.SteppingGap:
                {
                    // Gentle subtle buoyant bobbing for stepping platforms
                    float bob = Mathf.Sin(t * 1.5f) * 0.08f;
                    transform.localPosition = startLocalPos + Vector3.up * bob;
                    break;
                }
            }

            // Compute delta velocity for carrying CharacterController
            if (Time.deltaTime > 0.0001f)
            {
                Velocity = (transform.position - prevWorldPos) / Time.deltaTime;
                float deltaAngle = (transform.eulerAngles.y - prevWorldRot.eulerAngles.y);
                AngularVelocityY = deltaAngle / Time.deltaTime;
            }

            prevWorldPos = transform.position;
            prevWorldRot = transform.rotation;
        }

        public void Configure(MovingPlatformType type, Vector3 axis, float distance, float speed, float phase, float rotSpeed = 35f)
        {
            platformType = type;
            localMoveAxis = axis;
            travelDistance = distance;
            moveSpeed = speed;
            phaseOffset = phase;
            rotationSpeed = rotSpeed;
            startLocalPos = transform.localPosition;
            startLocalRot = transform.localRotation;
            prevWorldPos = transform.position;
            prevWorldRot = transform.rotation;
        }
    }
}
