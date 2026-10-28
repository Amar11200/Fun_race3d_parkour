using UnityEngine;

namespace DashMasters
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        public static ThirdPersonCamera Instance { get; private set; }

        [Header("Target & Offsets")]
        [SerializeField] private Transform target;
        [SerializeField] private float followDistance = 5.6f;
        [SerializeField] private float followHeight = 3.6f;
        [SerializeField] private float lookAheadDistance = 3.2f;
        [SerializeField] private float lookHeight = 1.2f;

        [Header("Smoothing")]
        [SerializeField] private float positionSmoothSpeed = 8.5f;
        [SerializeField] private float rotationSmoothSpeed = 8.5f;

        [Header("FOV Settings")]
        [SerializeField] private float defaultFov = 60f;
        [SerializeField] private float boostFov = 72f;
        [SerializeField] private float fovTransitionSpeed = 5f;

        private Camera cam;
        private float shakeIntensity;
        private float shakeDuration;
        private float targetFov;
        private bool isFocusingPodium = false;
        private Vector3 podiumCamPos;
        private Vector3 podiumLookTarget;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            cam = GetComponent<Camera>();
            targetFov = defaultFov;
        }

        private void LateUpdate()
        {
            if (isFocusingPodium)
            {
                transform.position = Vector3.Lerp(transform.position, podiumCamPos, Time.deltaTime * 3.5f);
                Quaternion rot = Quaternion.LookRotation(podiumLookTarget - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 3.5f);
                return;
            }

            if (target == null) return;

            // Follow behind target in local forward orientation
            Vector3 targetPosition = target.position;
            Vector3 forward = target.forward;
            if (forward == Vector3.zero) forward = Vector3.forward;

            PlayerController playerCtrl = target.GetComponent<PlayerController>() ?? target.GetComponentInParent<PlayerController>();
            bool isClimbing = playerCtrl != null && playerCtrl.IsClimbingLadder;

            float curFollowDist = isClimbing ? followDistance + 1.4f : followDistance;
            float curFollowHeight = isClimbing ? followHeight - 0.6f : followHeight;
            float curLookHeight = isClimbing ? lookHeight + 1.8f : lookHeight;

            Vector3 desiredPos = targetPosition - (forward * curFollowDist) + (Vector3.up * curFollowHeight);

            // Apply camera shake if active
            if (shakeDuration > 0f)
            {
                shakeDuration -= Time.deltaTime;
                desiredPos += Random.insideUnitSphere * shakeIntensity;
            }

            // Smooth position follow
            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * positionSmoothSpeed);

            // Look slightly ahead of player (elevated higher up tower face during climbing)
            Vector3 lookPoint = targetPosition + (forward * lookAheadDistance) + (Vector3.up * curLookHeight);
            Quaternion targetRotation = Quaternion.LookRotation(lookPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);

            // Smooth FOV
            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * fovTransitionSpeed);
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            isFocusingPodium = false;
            if (target != null)
            {
                Vector3 forward = target.forward != Vector3.zero ? target.forward : Vector3.forward;
                transform.position = target.position - (forward * followDistance) + (Vector3.up * followHeight);
                Vector3 lookPoint = target.position + (forward * lookAheadDistance) + (Vector3.up * lookHeight);
                transform.LookAt(lookPoint);
            }
        }

        public void ResetCamera(Transform newTarget)
        {
            SetTarget(newTarget);
        }

        public void TriggerShake(float intensity, float duration)
        {
            shakeIntensity = intensity;
            shakeDuration = duration;
        }

        public void TriggerBoostFov(float multiplier, float duration)
        {
            targetFov = boostFov;
            CancelInvoke(nameof(ResetFov));
            Invoke(nameof(ResetFov), duration);
        }

        private void ResetFov()
        {
            targetFov = defaultFov;
        }

        public void FocusOnPodium(Vector3 podiumCenter, Vector3 forwardDir)
        {
            isFocusingPodium = true;
            podiumLookTarget = podiumCenter + Vector3.up * 1.2f;
            podiumCamPos = podiumCenter + (forwardDir * 6.5f) + Vector3.up * 2.8f;
        }
    }
}
