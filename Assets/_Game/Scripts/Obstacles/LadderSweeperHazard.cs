using UnityEngine;

namespace DashMasters
{
    /// <summary>
    /// Dedicated collision hazard component for the rotating sweeper bar on the vertical climbing ladder.
    /// Uses a physically accurate, tightly fitted trigger volume that damages racers only upon direct physical contact.
    /// Safe passage is guaranteed when the sweeper arm is rotated away from the ladder climbing lane.
    /// </summary>
    public class LadderSweeperHazard : MonoBehaviour
    {
        [Header("Parent Obstacle Reference")]
        public LadderRotatingObstacle parentObstacle;

        private void OnTriggerEnter(Collider other)
        {
            CheckAndDamage(other);
        }

        private void OnTriggerStay(Collider other)
        {
            CheckAndDamage(other);
        }

        private void CheckAndDamage(Collider other)
        {
            if (parentObstacle == null || !parentObstacle.CanDamage) return;

            // Verify safe pass window as a redundant physical guarantee
            if (parentObstacle.IsSafeToPass()) return;

            Vector3 contact = other.ClosestPoint(transform.position);

            PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                Vector3 impactVel = parentObstacle.CalculateImpactVelocity(contact, player.transform);
                player.OnObstacleHit(contact, impactVel, parentObstacle.PushbackForce);
                return;
            }

            AIRacerController ai = other.GetComponent<AIRacerController>() ?? other.GetComponentInParent<AIRacerController>();
            if (ai != null)
            {
                Vector3 impactVel = parentObstacle.CalculateImpactVelocity(contact, ai.transform);
                ai.OnObstacleHit(contact, impactVel, parentObstacle.PushbackForce);
            }
        }
    }
}
