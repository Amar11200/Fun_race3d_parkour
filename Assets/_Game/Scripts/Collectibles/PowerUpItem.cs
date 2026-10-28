using UnityEngine;

namespace DashMasters
{
    public enum PowerUpType
    {
        SpeedBoost,
        Shield
    }

    public class PowerUpItem : MonoBehaviour
    {
        [Header("Power-Up Settings")]
        [SerializeField] private PowerUpType powerUpType = PowerUpType.SpeedBoost;
        [SerializeField] private float duration = 3.5f;
        [SerializeField] private float boostMultiplier = 1.6f;
        [SerializeField] private int scoreBonus = 100;

        [Header("Animation")]
        [SerializeField] private float rotationSpeed = 140f;
        [SerializeField] private float bobHeight = 0.14f;
        [SerializeField] private float bobSpeed = 3.2f;

        private Vector3 startPos;
        private bool isCollected = false;

        public PowerUpType Type => powerUpType;

        private void Start()
        {
            startPos = transform.position;
        }

        private void Update()
        {
            if (isCollected) return;

            // Spin
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

            // Floating bobbing motion
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCollected) return;

            PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                CollectForPlayer(player);
                return;
            }

            AIRacerController ai = other.GetComponent<AIRacerController>() ?? other.GetComponentInParent<AIRacerController>();
            if (ai != null)
            {
                CollectForAI(ai);
            }
        }

        private void CollectForPlayer(PlayerController player)
        {
            isCollected = true;

            if (powerUpType == PowerUpType.SpeedBoost)
            {
                player.ApplySpeedBoost(duration, boostMultiplier);
            }
            else if (powerUpType == PowerUpType.Shield)
            {
                player.ApplyShield();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(scoreBonus);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBoostSound();
            }

            if (ProceduralFX.Instance != null)
            {
                ProceduralFX.Instance.SpawnCoinBurst(transform.position);
            }

            gameObject.SetActive(false);
        }

        private void CollectForAI(AIRacerController ai)
        {
            isCollected = true;

            if (powerUpType == PowerUpType.SpeedBoost)
            {
                ai.ApplySpeedBoost(duration, boostMultiplier);
            }
            else if (powerUpType == PowerUpType.Shield)
            {
                ai.ApplyShield();
            }

            if (ProceduralFX.Instance != null)
            {
                ProceduralFX.Instance.SpawnCoinBurst(transform.position);
            }

            gameObject.SetActive(false);
        }

        public void Configure(PowerUpType type, float dur = 3.5f, float mult = 1.6f)
        {
            powerUpType = type;
            duration = dur;
            boostMultiplier = mult;
        }

        public void ResetPowerUp()
        {
            isCollected = false;
            gameObject.SetActive(true);
            if (startPos != Vector3.zero)
            {
                transform.position = startPos;
            }
        }
    }
}
