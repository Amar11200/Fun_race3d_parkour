using UnityEngine;

namespace DashMasters
{
    public class RotatingLog : ObstacleBase
    {
        [Header("Log Rotation")]
        [SerializeField] private float rollSpeed = 240f;
        [SerializeField] private Vector3 rollAxis = Vector3.right;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                return;
            }

            transform.Rotate(rollAxis.normalized, rollSpeed * Time.deltaTime, Space.Self);
        }

        public void Configure(float speed, Vector3 axis)
        {
            rollSpeed = speed;
            rollAxis = axis;
        }
    }
}
