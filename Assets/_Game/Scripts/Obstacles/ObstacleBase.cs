using UnityEngine;

namespace DashMasters
{
    public class ObstacleBase : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] protected bool canDamage = true;
        [SerializeField] protected float pushbackForce = 6.5f;

        public float PushbackForce { get => pushbackForce; set => pushbackForce = value; }
        public bool CanDamage { get => canDamage; set => canDamage = value; }

        protected virtual void Start()
        {
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleCollision(other, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 contact = collision.contactCount > 0 ? collision.GetContact(0).point : collision.collider.ClosestPoint(transform.position);
            HandleCollision(collision.collider, contact);
        }

        public virtual Vector3 CalculateImpactVelocity(Vector3 contactPoint, Transform racerTransform)
        {
            Vector3 diff = (racerTransform.position - transform.position);
            diff.y = 0f;
            if (diff.sqrMagnitude < 0.001f) diff = -racerTransform.forward;
            return (diff.normalized + Vector3.up * 0.4f).normalized * pushbackForce;
        }

        protected virtual void HandleCollision(Collider other, Vector3 contactPoint)
        {
            if (!canDamage) return;

            // Player collision
            PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                Vector3 impactVel = CalculateImpactVelocity(contactPoint, player.transform);
                player.OnObstacleHit(contactPoint, impactVel, pushbackForce);
                return;
            }

            // AI collision
            AIRacerController ai = other.GetComponent<AIRacerController>() ?? other.GetComponentInParent<AIRacerController>();
            if (ai != null)
            {
                Vector3 impactVel = CalculateImpactVelocity(contactPoint, ai.transform);
                ai.OnObstacleHit(contactPoint, impactVel, pushbackForce);
            }
        }
    }
}
