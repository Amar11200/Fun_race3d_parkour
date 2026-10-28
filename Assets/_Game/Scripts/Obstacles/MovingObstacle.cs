using UnityEngine;

namespace DashMasters
{
    public class MovingObstacle : ObstacleBase
    {
        [Header("Movement Configuration")]
        [SerializeField] private Vector3 moveDirection = Vector3.right;
        [SerializeField] private float travelDistance = 2.8f;
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float phaseOffset = 0f;

        private Vector3 startLocalPos;

        protected override void Start()
        {
            base.Start();
            startLocalPos = transform.localPosition;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            float offset = Mathf.Sin((Time.time * moveSpeed) + phaseOffset) * travelDistance;
            transform.localPosition = startLocalPos + (moveDirection.normalized * offset);
        }

        public void Configure(Vector3 direction, float distance, float speed, float phase)
        {
            moveDirection = direction;
            travelDistance = distance;
            moveSpeed = speed;
            phaseOffset = phase;
            startLocalPos = transform.localPosition;
        }
    }
}
