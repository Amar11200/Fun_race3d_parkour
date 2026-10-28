using UnityEngine;

namespace DashMasters
{
    public class SpeedBoostPad : MonoBehaviour
    {
        [Header("Boost Settings")]
        [SerializeField] private float boostDuration = 3.2f;
        [SerializeField] private float boostMultiplier = 1.6f;

        [Header("Visual Pulse")]
        [SerializeField] private MeshRenderer padRenderer;
        [SerializeField] private Color normalColor = new Color(0.1f, 0.9f, 0.4f);
        [SerializeField] private Color pulseColor = new Color(0.4f, 1f, 0.8f);

        private Material padMaterial;

        private void Start()
        {
            if (padRenderer != null)
            {
                padMaterial = padRenderer.material;
            }
        }

        private void Update()
        {
            if (padMaterial != null)
            {
                float t = Mathf.PingPong(Time.time * 3f, 1f);
                Color c = Color.Lerp(normalColor, pulseColor, t);
                padMaterial.color = c;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null) player = other.GetComponentInParent<PlayerController>();

            if (player != null)
            {
                player.ApplySpeedBoost(boostDuration, boostMultiplier);
                return;
            }
        }

        public void Configure(float duration, float multiplier, MeshRenderer mr = null)
        {
            boostDuration = duration;
            boostMultiplier = multiplier;
            if (mr != null) padRenderer = mr;
        }
    }
}
