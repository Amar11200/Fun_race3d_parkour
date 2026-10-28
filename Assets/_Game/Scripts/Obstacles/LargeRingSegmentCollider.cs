using UnityEngine;

namespace DashMasters
{
    /// <summary>
    /// Attached to each rotating/swinging segment of the Large Pink Segmented Ring.
    /// Ensures 100% reliable trigger detection in Unity physics.
    /// </summary>
    public class LargeRingSegmentCollider : MonoBehaviour
    {
        public ObstacleBase parentObstacle;
        public LargePinkSegmentedRing parentRing;

        private void OnTriggerEnter(Collider other)
        {
            ObstacleBase target = parentObstacle != null ? parentObstacle : parentRing;
            if (target == null || !target.CanDamage) return;

            Vector3 contact = other.ClosestPoint(transform.position);

            PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                Vector3 impactVel = target.CalculateImpactVelocity(contact, player.transform);
                player.OnObstacleHit(contact, impactVel, target.PushbackForce);
                return;
            }

            AIRacerController ai = other.GetComponent<AIRacerController>() ?? other.GetComponentInParent<AIRacerController>();
            if (ai != null)
            {
                Vector3 impactVel = target.CalculateImpactVelocity(contact, ai.transform);
                ai.OnObstacleHit(contact, impactVel, target.PushbackForce);
            }
        }
    }
}
