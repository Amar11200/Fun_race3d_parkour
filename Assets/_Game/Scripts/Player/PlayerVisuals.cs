using UnityEngine;

namespace DashMasters
{
    public class PlayerVisuals : MonoBehaviour
    {
        [Header("Limbs Hierarchy")]
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;

        [Header("Animation Settings")]
        [SerializeField] private float runCycleSpeed = 15f;
        [SerializeField] private float legSwingAngle = 42f;
        [SerializeField] private float armSwingAngle = 38f;
        [SerializeField] private float bounceHeight = 0.07f;

        private PlayerController player;
        private float animTime;
        private Vector3 initialTorsoPos;
        private bool isCelebration = false;
        private bool isKnockedOut = false;
        private bool isFalling = false;
        private bool isSliding = false;
        private bool isClimbing = false;
        private bool isClimbingHold = false;
        private bool isSliderChute = false;
        private bool isStumbling = false;
        private float stumbleTimer = 0f;

        private void Start()
        {
            player = GetComponentInParent<PlayerController>();
            if (torso != null) initialTorsoPos = torso.localPosition;
        }

        private void Update()
        {
            if (isCelebration)
            {
                AnimateVictoryDance();
                return;
            }

            if (isFalling)
            {
                AnimateFlail();
                return;
            }

            if (isStumbling)
            {
                AnimateStumble();
                stumbleTimer -= Time.deltaTime;
                if (stumbleTimer <= 0f) isStumbling = false;
                return;
            }

            if (isKnockedOut)
            {
                AnimateKnockout();
                return;
            }

            bool isRacing = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Racing;
            bool isMoving = isRacing && player != null && player.IsMovingForward;
            bool isGrounded = player != null ? player.IsGrounded : true;

            if (isSliderChute)
            {
                AnimateSliderChute();
                return;
            }

            if (isSliding)
            {
                AnimateCrawl();
                return;
            }

            if (isClimbingHold)
            {
                AnimateClimbHold();
                return;
            }

            if (isClimbing && isMoving)
            {
                AnimateClimb();
                return;
            }

            if (!isGrounded)
            {
                AnimateJump();
                return;
            }

            if (!isMoving)
            {
                // Idle gentle breathing
                ResetLimbs();
                if (torso != null)
                {
                    torso.localPosition = initialTorsoPos + Vector3.up * (Mathf.Sin(Time.time * 3.2f) * 0.02f);
                }
                return;
            }

            // Dynamic stride cadence scaling with actual racer speed
            float speedRatio = 1.0f;
            if (player != null && player.BaseSpeed > 0.1f)
            {
                speedRatio = Mathf.Clamp(player.CurrentSpeed / player.BaseSpeed, 0.6f, 1.85f);
            }
            animTime += Time.deltaTime * runCycleSpeed * speedRatio;

            // Running Cycle: Coordinated arm and leg stride
            float legSin = Mathf.Sin(animTime);
            float legAngle = legSin * legSwingAngle;

            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(legAngle, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-legAngle, 0f, 0f);

            // Foot planting: keep foot flat against road when leg is down/back
            if (leftFoot != null) leftFoot.localRotation = Quaternion.Euler(Mathf.Clamp(-legAngle * 0.6f, -20f, 20f), 0f, 0f);
            if (rightFoot != null) rightFoot.localRotation = Quaternion.Euler(Mathf.Clamp(legAngle * 0.6f, -20f, 20f), 0f, 0f);

            // Inverse arm swing
            float armAngle = legSin * armSwingAngle;
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-armAngle, 0f, 0f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(armAngle, 0f, 0f);

            // Torso sprint bounce and banking lean into turns
            if (torso != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(animTime)) * bounceHeight;
                torso.localPosition = initialTorsoPos + Vector3.up * bounce;

                // Responsive banking lean based on lateral motion
                float lateralTilt = 0f;
                if (player != null)
                {
                    lateralTilt = Mathf.Clamp(player.LateralVelocity * 2.8f, -14f, 14f);
                }
                float sprintForwardAngle = Mathf.Lerp(7f, 14f, (speedRatio - 1f) / 0.85f);
                torso.localRotation = Quaternion.Euler(sprintForwardAngle, 0f, -lateralTilt);
            }

            // Head bob
            if (head != null)
            {
                head.localRotation = Quaternion.Euler(Mathf.Sin(animTime * 2f) * 4f, 0f, 0f);
            }
        }

        private void AnimateJump()
        {
            // Dynamic 3-phase jump based on vertical velocity
            float vy = player != null ? player.VerticalVelocity : 0f;

            if (vy > 1.6f)
            {
                // Phase 1: Ascent / Launch - knees tuck up, arms thrust forward-up for upward momentum
                float tAscent = Mathf.Clamp01(vy / 8.0f);
                float legAngle = Mathf.Lerp(25f, 48f, tAscent);
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(legAngle, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(legAngle + 3f, 0f, 0f);
                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-80f, 0f, -24f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-80f, 0f, 24f);
                if (torso != null)
                {
                    torso.localPosition = initialTorsoPos + Vector3.up * 0.10f;
                    torso.localRotation = Quaternion.Euler(-7f, 0f, 0f); // slight backward arch
                }
            }
            else if (vy > -1.6f)
            {
                // Phase 2: Apex - aerodynamic tuck, arms spreading outward for balance
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(36f, 0f, -6f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(38f, 0f, 6f);
                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-55f, 0f, -42f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-55f, 0f, 42f);
                if (torso != null)
                {
                    torso.localPosition = initialTorsoPos + Vector3.up * 0.08f;
                    torso.localRotation = Quaternion.identity;
                }
            }
            else
            {
                // Phase 3: Descent / Falling - legs extending downward anticipating ground impact
                float tDescent = Mathf.Clamp01(-vy / 12.0f);
                float legExtend = Mathf.Lerp(20f, -4f, tDescent);
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(legExtend, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(legExtend, 0f, 0f);
                if (leftFoot != null) leftFoot.localRotation = Quaternion.Euler(15f, 0f, 0f);
                if (rightFoot != null) rightFoot.localRotation = Quaternion.Euler(15f, 0f, 0f);
                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-35f, 0f, -28f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-35f, 0f, 28f);
                if (torso != null)
                {
                    torso.localPosition = initialTorsoPos + Vector3.up * 0.04f;
                    torso.localRotation = Quaternion.Euler(5f, 0f, 0f);
                }
            }
        }

        private void AnimateCrawl()
        {
            // Sliding / crawling low to ground
            float t = Time.time * 18f;
            if (torso != null)
            {
                torso.localPosition = initialTorsoPos + new Vector3(0f, -0.38f, 0.22f);
                torso.localRotation = Quaternion.Euler(55f, 0f, 0f); // tilted forward low
            }
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-80f + Mathf.Sin(t) * 15f, 0f, -12f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-80f - Mathf.Sin(t) * 15f, 0f, 12f);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(75f - Mathf.Sin(t) * 20f, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(75f + Mathf.Sin(t) * 20f, 0f, 0f);
        }

        private void AnimateStumble()
        {
            // Dynamic stagger reaction to impact: off-balance pitch and recovery
            float stumbleProgress = stumbleTimer > 0f ? Mathf.Clamp01(1f - (stumbleTimer / 0.45f)) : 1f;
            float wobble = Mathf.Sin(stumbleProgress * Mathf.PI * 3f);
            if (torso != null)
            {
                torso.localPosition = initialTorsoPos + new Vector3(wobble * 0.08f, 0.04f, -0.16f);
                torso.localRotation = Quaternion.Euler(-22f, wobble * 18f, wobble * 14f);
            }
            if (head != null) head.localRotation = Quaternion.Euler(-18f, wobble * 20f, 0f);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-60f + wobble * 30f, 0f, -50f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-75f - wobble * 30f, 0f, 50f);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(-25f, 0f, -10f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(30f, 0f, 10f);
        }

        private void AnimateFlail()
        {
            float t = Time.time * 26f;
            // Wild asymmetrical flailing as racer tumbles into the water
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(Mathf.Sin(t) * 85f, Mathf.Cos(t * 0.7f) * 45f, -42f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-Mathf.Sin(t * 1.1f) * 85f, -Mathf.Cos(t * 0.7f) * 45f, 42f);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(Mathf.Cos(t) * 70f, 0f, -22f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-Mathf.Cos(t * 0.9f) * 70f, 0f, 22f);
            if (torso != null)
            {
                torso.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.6f) * 25f, Mathf.Cos(t * 0.5f) * 28f, Mathf.Sin(t * 0.8f) * 18f);
            }
            if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.6f) * 32f, Mathf.Cos(t) * 30f, 0f);
        }

        private void AnimateVictoryDance()
        {
            float t = Time.time * 7f;
            float hop = Mathf.Max(0f, Mathf.Sin(t * 1.6f)) * 0.22f;
            if (torso != null)
            {
                torso.localPosition = initialTorsoPos + Vector3.up * hop;
                torso.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 6f, 0f, 0f);
            }

            // Alternating victory arm pumps
            float armWave = Mathf.Sin(t * 1.6f) * 25f;
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-155f + armWave, 0f, -24f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-155f - armWave, 0f, 24f);

            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(hop > 0.05f ? 22f : 0f, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(hop > 0.05f ? -14f : 0f, 0f, 0f);
            if (head != null) head.localRotation = Quaternion.Euler(-10f + Mathf.Sin(t * 1.6f) * 14f, 0f, 0f);
        }

        private void AnimateKnockout()
        {
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(50f, 0f, 35f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(50f, 0f, -35f);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(25f, 0f, 0f);
        }

        public void SetCelebration(bool celebrate)
        {
            isCelebration = celebrate;
            isKnockedOut = false;
            isFalling = false;
            isStumbling = false;
            if (!celebrate) ResetLimbs();
        }

        public void SetStumble(bool stumble, float duration = 0.25f)
        {
            isStumbling = stumble;
            stumbleTimer = duration;
        }

        public void SetKnockout(bool knockedOut)
        {
            isKnockedOut = knockedOut;
            if (!knockedOut) ResetLimbs();
        }

        public void SetFalling(bool falling)
        {
            isFalling = falling;
            if (!falling) ResetLimbs();
        }

        public void SetSliding(bool sliding)
        {
            isSliding = sliding;
            if (!sliding)
            {
                if (torso != null)
                {
                    torso.localPosition = initialTorsoPos;
                    torso.localRotation = Quaternion.identity;
                }
                ResetLimbs();
            }
        }

        public void SetClimbing(bool climbing)
        {
            isClimbing = climbing;
            if (!climbing) isClimbingHold = false;
        }

        public void SetClimbingHold(bool holding)
        {
            isClimbingHold = holding;
        }

        public void SetSliderChute(bool inChute)
        {
            isSliderChute = inChute;
            if (!inChute)
            {
                if (torso != null)
                {
                    torso.localPosition = initialTorsoPos;
                    torso.localRotation = Quaternion.identity;
                }
                ResetLimbs();
            }
        }

        private void AnimateClimbHold()
        {
            // Stable grip holding onto ladder rungs while waiting
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(25f, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-65f, 0f, -12f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-45f, 0f, 12f);

            if (torso != null)
            {
                torso.localPosition = initialTorsoPos + new Vector3(0f, -0.05f, 0.12f);
                torso.localRotation = Quaternion.Euler(15f, 0f, 0f);
            }
            if (head != null) head.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        }

        private void AnimateSliderChute()
        {
            // Thrilling water-slide sitting posture (matching reference screenshot)
            // Sitting low on the slide, legs extended straight down the chute, arms back for balance
            float jitter = Mathf.Sin(Time.time * 20f) * 0.02f;
            if (torso != null)
            {
                torso.localPosition = initialTorsoPos + new Vector3(0f, -0.38f + jitter, -0.05f);
                torso.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            }
            // Legs extended forward and slightly outward down the slide
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(-75f, -6f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-75f, 6f, 0f);

            // Arms positioned slightly back and out, bracing on the slide chute
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(30f, 0f, 28f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(30f, 0f, -28f);

            if (head != null) head.localRotation = Quaternion.Euler(10f, 0f, 0f);
        }

        private void AnimateClimb()
        {
            animTime += Time.deltaTime * (runCycleSpeed * 1.15f);
            float legSin = Mathf.Sin(animTime);
            float legAngle = legSin * 50f;

            // Deep high steps
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(legAngle, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-legAngle, 0f, 0f);

            // Powerful arm reaches
            float armAngle = legSin * 48f;
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-armAngle - 25f, 0f, -10f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(armAngle - 25f, 0f, 10f);

            // Torso leaned heavily forward into the slope
            if (torso != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(animTime)) * (bounceHeight * 1.3f);
                torso.localPosition = initialTorsoPos + Vector3.up * bounce + new Vector3(0f, -0.1f, 0.15f);
                torso.localRotation = Quaternion.Euler(26f, 0f, 0f);
            }
        }

        private void ResetLimbs()
        {
            if (leftLeg != null) leftLeg.localRotation = Quaternion.identity;
            if (rightLeg != null) rightLeg.localRotation = Quaternion.identity;
            if (leftFoot != null) leftFoot.localRotation = Quaternion.identity;
            if (rightFoot != null) rightFoot.localRotation = Quaternion.identity;
            if (leftArm != null) leftArm.localRotation = Quaternion.identity;
            if (rightArm != null) rightArm.localRotation = Quaternion.identity;
            if (leftHand != null) leftHand.localRotation = Quaternion.identity;
            if (rightHand != null) rightHand.localRotation = Quaternion.identity;
            if (head != null) head.localRotation = Quaternion.identity;
            if (torso != null) torso.localRotation = Quaternion.identity;
        }

        public void AssignLimbs(Transform torsoT, Transform headT, Transform lLeg, Transform rLeg, Transform lArm, Transform rArm,
            Transform lFoot = null, Transform rFoot = null, Transform lHand = null, Transform rHand = null)
        {
            torso = torsoT;
            head = headT;
            leftLeg = lLeg;
            rightLeg = rLeg;
            leftArm = lArm;
            rightArm = rArm;
            leftFoot = lFoot;
            rightFoot = rFoot;
            leftHand = lHand;
            rightHand = rHand;
            if (torso != null) initialTorsoPos = torso.localPosition;
        }
    }
}
