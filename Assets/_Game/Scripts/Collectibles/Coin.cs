using UnityEngine;

namespace DashMasters
{
    public class Coin : MonoBehaviour
    {
        [Header("Coin Values")]
        [SerializeField] private int coinValue = 1;
        [SerializeField] private int scoreValue = 50;

        [Header("Animation")]
        [SerializeField] private float rotationSpeed = 160f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 3.6f;

        private Vector3 startPos;
        private bool isCollected = false;

        private void Awake()
        {
            if (startPos == Vector3.zero)
            {
                startPos = transform.position;
            }
        }

        private void Start()
        {
            if (startPos == Vector3.zero)
            {
                startPos = transform.position;
            }
        }

        public void SetStartPosition(Vector3 pos)
        {
            startPos = pos;
            transform.position = pos;
        }

        private void Update()
        {
            if (isCollected) return;

            // Spin
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

            // Floating bob along local vertical offset
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCollected) return;

            PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                Collect(true);
                return;
            }

            AIRacerController ai = other.GetComponent<AIRacerController>() ?? other.GetComponentInParent<AIRacerController>();
            if (ai != null)
            {
                Collect(false);
            }
        }

        private void Collect(bool isPlayer)
        {
            isCollected = true;

            if (isPlayer)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddCoins(coinValue);
                    GameManager.Instance.AddScore(scoreValue);
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayCoinSound();
                }
            }

            if (ProceduralFX.Instance != null)
            {
                ProceduralFX.Instance.SpawnCoinBurst(transform.position);
            }

            gameObject.SetActive(false);
        }

        public void ResetCoin()
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
