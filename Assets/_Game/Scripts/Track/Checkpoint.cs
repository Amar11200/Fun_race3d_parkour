using UnityEngine;

namespace DashMasters
{
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private int checkpointIndex;
        [SerializeField] private float trackDistance;
        private bool isPassed = false;

        public bool IsPassed => isPassed;

        public int CheckpointIndex => checkpointIndex;
        public float TrackDistance { get => trackDistance; set => trackDistance = value; }

        private void Start()
        {
            if (TrackPath.Instance != null)
            {
                TrackPath.Instance.RegisterCheckpoint(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.SetCheckpoint(transform.position + Vector3.up * 0.25f, transform.rotation, trackDistance);
                isPassed = true;
                return;
            }

            AIRacerController ai = other.GetComponent<AIRacerController>() ?? other.GetComponentInParent<AIRacerController>();
            if (ai != null)
            {
                ai.SetCheckpoint(transform.position + Vector3.up * 0.25f, trackDistance);
            }
        }

        public void Configure(int index, float distance)
        {
            checkpointIndex = index;
            trackDistance = distance;
            isPassed = false;
        }

        public void ResetCheckpoint()
        {
            isPassed = false;
        }
    }
}
