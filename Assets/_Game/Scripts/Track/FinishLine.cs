using System.Collections.Generic;
using UnityEngine;

namespace DashMasters
{
    public class FinishLine : MonoBehaviour
    {
        private List<string> finishedRacers = new List<string>();
        private bool playerFinished = false;

        private void OnTriggerEnter(Collider other)
        {
            // Check player
            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null) player = other.GetComponentInParent<PlayerController>();

            if (player != null && !playerFinished)
            {
                playerFinished = true;
                finishedRacers.Add("Player");
                int rank = finishedRacers.Count;

                if (ProceduralFX.Instance != null)
                {
                    ProceduralFX.Instance.SpawnConfetti(transform.position + Vector3.up * 3f + Vector3.left * 2f);
                    ProceduralFX.Instance.SpawnConfetti(transform.position + Vector3.up * 3f + Vector3.right * 2f);
                }

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnPlayerFinished(rank);
                }
                return;
            }

            // Check AI
            AIRacerController ai = other.GetComponent<AIRacerController>();
            if (ai == null) ai = other.GetComponentInParent<AIRacerController>();

            if (ai != null && !finishedRacers.Contains(ai.RacerName))
            {
                finishedRacers.Add(ai.RacerName);
                int rank = finishedRacers.Count;

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnAIFinished(ai.RacerName, rank);
                }
            }
        }

        public void ResetFinishLine()
        {
            finishedRacers.Clear();
            playerFinished = false;
        }
    }
}
