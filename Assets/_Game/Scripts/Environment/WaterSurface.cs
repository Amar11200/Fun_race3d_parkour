using UnityEngine;

namespace DashMasters
{
    /// <summary>
    /// Manages the massive continuous turquoise/blue water surface surrounding the course.
    /// Provides subtle procedural wave animation, UV shimmering, and triggers water splash FX
    /// whenever any racer reaches the waterline.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class WaterSurface : MonoBehaviour
    {
        [Header("Water Shimmer & Flow")]
        [SerializeField] private Vector2 flowSpeed = new Vector2(0.018f, 0.026f);
        [SerializeField] private Color deepBlue = new Color(0.02f, 0.62f, 0.88f);
        [SerializeField] private Color turquoiseShimmer = new Color(0.08f, 0.82f, 0.96f);

        private Material waterMat;
        private Vector2 currentOffset;
        private BoxCollider waterCollider;

        public void SetColors(Color deep, Color shimmer)
        {
            deepBlue = deep;
            turquoiseShimmer = shimmer;
            if (waterMat == null)
            {
                MeshRenderer mr = GetComponent<MeshRenderer>();
                if (mr != null) waterMat = mr.material;
            }
            if (waterMat != null)
            {
                if (waterMat.HasProperty("_BaseColor")) waterMat.SetColor("_BaseColor", deep);
                else if (waterMat.HasProperty("_Color")) waterMat.SetColor("_Color", deep);
            }
        }

        private void Start()
        {
            waterCollider = GetComponent<BoxCollider>();
            if (waterCollider != null)
            {
                waterCollider.isTrigger = true;
            }

            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                waterMat = mr.material;
            }
        }

        private void Update()
        {
            if (waterMat == null) return;

            currentOffset += flowSpeed * Time.deltaTime;
            if (waterMat.HasProperty("_BaseMap"))
            {
                waterMat.SetTextureOffset("_BaseMap", currentOffset);
            }
            else if (waterMat.HasProperty("_MainTex"))
            {
                waterMat.SetTextureOffset("_MainTex", currentOffset);
            }

            // Subtle glistening wave oscillation
            float shimmer = (Mathf.Sin(Time.time * 1.8f) + 1f) * 0.5f;
            Color liveColor = Color.Lerp(deepBlue, turquoiseShimmer, shimmer * 0.25f);
            if (waterMat.HasProperty("_BaseColor"))
            {
                waterMat.SetColor("_BaseColor", liveColor);
            }
            else if (waterMat.HasProperty("_Color"))
            {
                waterMat.SetColor("_Color", liveColor);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.OnWaterSurfaceReached(transform.position.y);
                return;
            }

            AIRacerController ai = other.GetComponent<AIRacerController>() ?? other.GetComponentInParent<AIRacerController>();
            if (ai != null)
            {
                ai.OnWaterSurfaceReached(transform.position.y);
            }
        }
    }
}
