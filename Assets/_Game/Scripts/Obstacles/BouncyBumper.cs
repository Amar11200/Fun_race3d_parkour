using System.Collections;
using UnityEngine;

namespace DashMasters
{
    public class BouncyBumper : ObstacleBase
    {
        [Header("Bounce Settings")]
        [SerializeField] private float bounceKnockback = 8f;
        [SerializeField] private Transform visualModel;

        private Vector3 initialScale = Vector3.one;
        private bool isBouncing = false;

        protected override void Start()
        {
            base.Start();
            if (visualModel != null)
            {
                initialScale = visualModel.localScale;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleBumperHit(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleBumperHit(other);
        }

        private void HandleBumperHit(Collider other)
        {
            // Trigger visual squash
            if (!isBouncing && visualModel != null)
            {
                StartCoroutine(SquashRoutine());
            }

            // Push player back
            PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.OnObstacleHit(transform.position);
                return;
            }

            // Push AI back
            AIRacerController ai = other.GetComponent<AIRacerController>() ?? other.GetComponentInParent<AIRacerController>();
            if (ai != null)
            {
                ai.OnObstacleHit(transform.position);
            }
        }

        private IEnumerator SquashRoutine()
        {
            isBouncing = true;
            float t = 0f;
            float duration = 0.25f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = t / duration;
                float scaleMod = 1f + Mathf.Sin(progress * Mathf.PI) * 0.4f;
                if (visualModel != null)
                {
                    visualModel.localScale = new Vector3(initialScale.x * scaleMod, initialScale.y / scaleMod, initialScale.z * scaleMod);
                }
                yield return null;
            }

            if (visualModel != null) visualModel.localScale = initialScale;
            isBouncing = false;
        }

        public void Configure(Transform model, float knockback)
        {
            visualModel = model;
            bounceKnockback = knockback;
            if (visualModel != null) initialScale = visualModel.localScale;
        }
    }
}
