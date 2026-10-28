using System.Collections;
using UnityEngine;

namespace DashMasters
{
    [RequireComponent(typeof(CharacterController))]
    public class AIRacerController : MonoBehaviour
    {
        [Header("AI Identity")]
        [SerializeField] private string racerName = "Racer Cobalt";
        [SerializeField] private Color racerColor = new Color(0.1f, 0.7f, 1.0f);

        [Header("Speed & Path Settings")]
        [SerializeField] private float baseSpeed = 7.3f;
        [SerializeField] private float lateralOffset = -1.1f;
        private float defaultLateralOffset = 0f;
        [SerializeField] private float jumpHeight = 2.0f;
        [SerializeField] private float gravity = 26f;
        [SerializeField] private float slideHeight = 0.55f;

        [Header("AI Obstacle Sensor")]
        [SerializeField] private float lookAheadDistance = 5.5f;
        [SerializeField] private float mistakeChance = 0.06f;

        [Header("Visual References")]
        [SerializeField] private Transform characterModel;
        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        private CharacterController controller;
        private float originalHeight;
        private Vector3 originalCenter;

        private float currentTrackDistance = 0f;
        private float lastCheckpointDist = 0f;
        private Vector3 startPosition;
        private float verticalVelocity = 0f;

        private bool isWaitingForObstacle = false;
        private bool isSliding = false;
        private bool isJumping = false;
        private bool isClimbingLadder = false;
        private bool isSliderChute = false;
        private bool isHoldingLadder = false;
        private float jumpCooldownTimer = 0f;
        private float ladderExitGraceTimer = 0f;

        private bool isBoosted = false;
        private float boostTimer = 0f;
        private float boostMultiplier = 1.5f;
        private bool hasShield = false;
        private GameObject activeShieldVisual;
        private float stumbleTimer = 0f;
        private bool isOvertaking = false;

        private bool isKnockedOut = false;
        private bool isFalling = false;
        private bool hasHitWater = false;
        private bool isCelebration = false;
        private Vector3 knockoutVelocity;
        private Vector3 tumbleTorque;
        private Renderer[] cachedRenderers;
        private float forwardSlope = 0f;

        private float animTime;
        private Vector3 initialTorsoPos;

        private int consecutiveDeathsAtSamePoint = 0;
        private float lastDeathDist = -999f;

        public string RacerName => racerName;
        public Color RacerColor => racerColor;
        public float CurrentTrackDistance => currentTrackDistance;
        public bool IsBoosted => isBoosted;
        public bool HasShield => hasShield;
        public float CurrentSpeed
        {
            get
            {
                if (isWaitingForObstacle) return 0f;
                float spd = baseSpeed;
                if (isBoosted) spd *= boostMultiplier;
                if (isOvertaking) spd *= 1.12f;
                if (stumbleTimer > 0f) spd *= 0.55f;
                return spd;
            }
        }
        public bool IsSliding => isSliding;
        public bool IsJumping => isJumping;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            originalHeight = controller.height;
            originalCenter = controller.center;
            startPosition = transform.position;
            defaultLateralOffset = lateralOffset;
            if (torso != null) initialTorsoPos = torso.localPosition;
        }

        private void Update()
        {
            if (boostTimer > 0f)
            {
                boostTimer -= Time.deltaTime;
                if (boostTimer <= 0f) isBoosted = false;
            }

            if (stumbleTimer > 0f)
            {
                stumbleTimer -= Time.deltaTime;
            }

            if (ladderExitGraceTimer > 0f)
            {
                ladderExitGraceTimer -= Time.deltaTime;
            }

            if (jumpCooldownTimer > 0f)
            {
                jumpCooldownTimer -= Time.deltaTime;
            }

            if (isCelebration)
            {
                AnimateVictoryDance();
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Racing)
            {
                ApplyGravityOnly();
                return;
            }

            if (isKnockedOut || isFalling)
            {
                HandleKnockoutFall();
                return;
            }

            HandleAILogic();
            HandleTrackMovement();
            HandleVisuals();
        }

        private void ApplyGravityOnly()
        {
            if (controller.isGrounded)
            {
                verticalVelocity = -1f;
                isJumping = false;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }
            controller.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);
        }

        private void HandleAILogic()
        {
            if (isClimbingLadder) return; // Dedicated ladder traversal logic in HandleTrackMovement

            bool shouldWait = false;
            bool shouldSlide = false;
            bool shouldJump = false;

            if (TrackPath.Instance == null) return;

            PathPoint pt = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
            Vector3 forwardDir = pt.forward != Vector3.zero ? pt.forward : transform.forward;
            Vector3 aiPos = transform.position;

            // Spherecast forward along track path direction to detect upcoming hazards
            Vector3 rayOrigin = aiPos + Vector3.up * 0.45f;
            Ray ray = new Ray(rayOrigin, forwardDir);
            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.65f, lookAheadDistance);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;

                float hitDist = hits[i].distance;
                Vector3 hitPoint = hits[i].point;

                // 1. ROTATING HAZARD (Red Sweeper, Rotating Turntable)
                RotatingHazard rotHazard = col.GetComponent<RotatingHazard>() ?? col.GetComponentInParent<RotatingHazard>();
                if (rotHazard != null)
                {
                    Vector3 sweeperPivot = rotHazard.transform.position;
                    float forwardDistToPivot = Vector3.Dot(sweeperPivot - aiPos, forwardDir);

                    // If approaching or inside the sweeper radius (-0.5m to 3.8m)
                    if (forwardDistToPivot > -0.6f && forwardDistToPivot < 3.8f)
                    {
                        // Check sweeper bar direction in horizontal XZ plane
                        Vector2 barDir2D = new Vector2(rotHazard.transform.right.x, rotHazard.transform.right.z).normalized;
                        Vector2 toAI2D = new Vector2(aiPos.x - sweeperPivot.x, aiPos.z - sweeperPivot.z);
                        float distToBarLine = Mathf.Abs(toAI2D.x * -barDir2D.y + toAI2D.y * barDir2D.x);
                        float projOnBar = Vector2.Dot(toAI2D, barDir2D);

                        float barHalfLen = 1.7f;
                        if (Mathf.Abs(projOnBar) < barHalfLen + 0.6f)
                        {
                            // If the rotating bar is currently obstructing the lane
                            if (distToBarLine < 1.45f)
                            {
                                if (forwardDistToPivot > 0.4f)
                                {
                                    shouldWait = true;
                                }
                                else if (controller.isGrounded)
                                {
                                    // Close range emergency jump over the sweeper
                                    shouldJump = true;
                                }
                            }
                        }
                    }
                }

                // 2. ROTATING HAMMER ARM
                RotatingHammerArm rotArm = col.GetComponent<RotatingHammerArm>() ?? col.GetComponentInParent<RotatingHammerArm>();
                if (rotArm != null)
                {
                    Vector3 armPivot = rotArm.transform.position;
                    float forwardDist = Vector3.Dot(armPivot - aiPos, forwardDir);
                    if (forwardDist > -0.6f && forwardDist < 4.0f)
                    {
                        Vector2 armDir2D = new Vector2(rotArm.transform.right.x, rotArm.transform.right.z).normalized;
                        Vector2 toAI2D = new Vector2(aiPos.x - armPivot.x, aiPos.z - armPivot.z);
                        float distToArmLine = Mathf.Abs(toAI2D.x * -armDir2D.y + toAI2D.y * armDir2D.x);
                        if (distToArmLine < 1.6f && forwardDist > 0.4f)
                        {
                            shouldWait = true;
                        }
                    }
                }

                // 3. OVERHEAD SWINGING HAMMER (Pendulum)
                PendulumHammer pendHammer = col.GetComponent<PendulumHammer>() ?? col.GetComponentInParent<PendulumHammer>();
                if (pendHammer != null)
                {
                    float forwardDist = Vector3.Dot(pendHammer.transform.position - aiPos, forwardDir);
                    if (forwardDist > -0.5f && forwardDist < 3.8f)
                    {
                        // Check if hammer head is currently low across track
                        if (col.bounds.min.y - aiPos.y < 2.2f && Mathf.Abs(col.transform.position.x - aiPos.x) < 1.6f)
                        {
                            if (forwardDist > 0.5f)
                            {
                                shouldWait = true;
                            }
                        }
                    }
                }

                // 4. WATER-RISING ROTATING BAR
                WaterRisingRotatingBar waterBar = col.GetComponent<WaterRisingRotatingBar>() ?? col.GetComponentInParent<WaterRisingRotatingBar>();
                if (waterBar != null)
                {
                    Vector3 barPivot = waterBar.transform.position;
                    float forwardDist = Vector3.Dot(barPivot - aiPos, forwardDir);
                    if (forwardDist > -0.6f && forwardDist < 4.2f)
                    {
                        Vector2 barDir2D = new Vector2(waterBar.transform.right.x, waterBar.transform.right.z).normalized;
                        Vector2 toAI2D = new Vector2(aiPos.x - barPivot.x, aiPos.z - barPivot.z);
                        float distToBar = Mathf.Abs(toAI2D.x * -barDir2D.y + toAI2D.y * barDir2D.x);
                        float proj = Vector2.Dot(toAI2D, barDir2D);
                        if (Mathf.Abs(proj) < waterBar.BarLength * 0.5f + 0.6f && distToBar < 1.55f)
                        {
                            if (forwardDist > 0.4f) shouldWait = true;
                            else if (controller.isGrounded) shouldJump = true;
                        }
                    }
                }

                // 5. CRUSHER GATE (Descending slab)
                CrusherGate crusher = col.GetComponent<CrusherGate>() ?? col.GetComponentInParent<CrusherGate>();
                if (crusher != null)
                {
                    float forwardDist = Vector3.Dot(crusher.transform.position - aiPos, forwardDir);
                    if (forwardDist > -0.4f && forwardDist < 4.0f)
                    {
                        float slabBottom = col.bounds.min.y - aiPos.y;
                        if (slabBottom < 0.50f)
                        {
                            // Slammed down on track - wait until it begins rising
                            if (forwardDist > 0.3f) shouldWait = true;
                        }
                        else if (slabBottom < 1.45f)
                        {
                            // Low slab - slide / crawl underneath
                            shouldSlide = true;
                        }
                    }
                }

                // 6. JUMP HURDLE, ROTATING LOG, & PLATFORM HAZARDS
                ObstacleBase obs = col.GetComponent<ObstacleBase>() ?? col.GetComponentInParent<ObstacleBase>();
                GreenPlatformHazard greenHazard = col.GetComponent<GreenPlatformHazard>() ?? col.GetComponentInParent<GreenPlatformHazard>();
                RotatingLog rotLog = col.GetComponent<RotatingLog>() ?? col.GetComponentInParent<RotatingLog>();

                if (col.name.Contains("Hurdle") || rotLog != null || greenHazard != null)
                {
                    if (hitDist > 1.2f && hitDist < 2.9f)
                    {
                        shouldJump = true;
                    }
                }

                // 7. C-SHAPED PINK SEGMENTED HOOPS & RINGS
                PinkSegmentedCRing cRing = col.GetComponent<PinkSegmentedCRing>() ?? col.GetComponentInParent<PinkSegmentedCRing>();
                PinkSegmentedHoop hoop = col.GetComponent<PinkSegmentedHoop>() ?? col.GetComponentInParent<PinkSegmentedHoop>();
                LargeRingSegmentCollider ringSeg = col.GetComponent<LargeRingSegmentCollider>();
                LargePinkSegmentedRing largeRing = ringSeg != null ? ringSeg.parentRing : (col.GetComponent<LargePinkSegmentedRing>() ?? col.GetComponentInParent<LargePinkSegmentedRing>());

                if (cRing != null || hoop != null || largeRing != null || ringSeg != null)
                {
                    // Ring lies on track at y ~ 0.38m; jump over oncoming rim or pass through opening
                    if (hitDist > 1.3f && hitDist < 2.8f)
                    {
                        shouldJump = true;
                    }
                }
            }

            // Mistake calculation: if AI already failed once here, eliminate mistake chance so it never gets stuck
            float effectiveMistakeChance = (consecutiveDeathsAtSamePoint >= 1) ? 0f : mistakeChance;
            bool makeMistake = Random.value < effectiveMistakeChance;

            // Apply decisions
            if (shouldWait && !makeMistake)
            {
                isWaitingForObstacle = true;
            }
            else
            {
                isWaitingForObstacle = false;
            }

            if (shouldSlide)
            {
                SetSliding(true);
            }
            else
            {
                SetSliding(false);
            }

            if (shouldJump && (!makeMistake || consecutiveDeathsAtSamePoint >= 1))
            {
                TryExecuteJump();
            }

            // DYNAMIC OVERTAKING & COMPETITIVE LANE DRAFTING
            float desiredLateral = defaultLateralOffset;
            isOvertaking = false;

            if (GameManager.Instance != null && !isClimbingLadder)
            {
                // 1. Scan Player ahead
                PlayerController p = GameManager.Instance.Player;
                if (p != null)
                {
                    float pDist = p.CurrentTrackDistance - currentTrackDistance;
                    if (pDist > 0.5f && pDist < 4.8f)
                    {
                        // Player is in our path or running slower/waiting
                        if (Mathf.Abs(p.transform.position.x - transform.position.x) < 0.95f || p.CurrentSpeed < CurrentSpeed * 0.9f)
                        {
                            isOvertaking = true;
                            // Steer into open space on opposite side of player
                            desiredLateral = (p.transform.position.x >= transform.position.x) ? -1.05f : 1.05f;
                        }
                    }
                }

                // 2. Scan other AI Racers ahead
                var allAI = GameManager.Instance.AIRacers;
                if (allAI != null && !isOvertaking)
                {
                    for (int a = 0; a < allAI.Count; a++)
                    {
                        var other = allAI[a];
                        if (other == null || other == this) continue;
                        float aiDist = other.CurrentTrackDistance - currentTrackDistance;
                        if (aiDist > 0.5f && aiDist < 4.2f)
                        {
                            if (Mathf.Abs(other.transform.position.x - transform.position.x) < 0.85f || other.CurrentSpeed < CurrentSpeed * 0.9f)
                            {
                                isOvertaking = true;
                                desiredLateral = (other.transform.position.x >= transform.position.x) ? -1.1f : 1.1f;
                                break;
                            }
                        }
                    }
                }
            }

            // Smooth lane adjustment
            if (ladderExitGraceTimer <= 0f && !isClimbingLadder)
            {
                float steerRate = isOvertaking ? 3.8f : 1.8f;
                lateralOffset = Mathf.MoveTowards(lateralOffset, desiredLateral, Time.deltaTime * steerRate);
                lateralOffset = Mathf.Clamp(lateralOffset, -1.25f, 1.25f);
            }

            // Fall check off exposed track into open air (never during ladder climb or exit grace period)
            if (transform.position.y < -1.8f && !isKnockedOut && !isClimbingLadder && ladderExitGraceTimer <= 0f)
            {
                isKnockedOut = true;
                knockoutVelocity = (transform.forward * CurrentSpeed * 0.45f) + (Vector3.down * 3.2f);
                tumbleTorque = new Vector3(85f, 65f, 40f);
                isFalling = true;
            }
            else if (transform.position.y <= -5.2f && !hasHitWater)
            {
                TriggerWaterFall();
            }
        }

        private void TryExecuteJump()
        {
            if (jumpCooldownTimer > 0f || isJumping) return;
            if (controller != null && controller.isGrounded)
            {
                isJumping = true;
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * gravity);
                jumpCooldownTimer = 0.45f;
            }
        }

        private void SetSliding(bool sliding)
        {
            if (isSliding == sliding) return;
            isSliding = sliding;
            if (controller != null)
            {
                if (sliding)
                {
                    controller.height = slideHeight;
                    controller.center = new Vector3(originalCenter.x, slideHeight * 0.5f, originalCenter.z);
                }
                else
                {
                    controller.height = originalHeight;
                    controller.center = originalCenter;
                }
            }
        }

        private void HandleTrackMovement()
        {
            // CRITICAL: Ground detection & gravity that preserves upward jump velocity
            RaycastHit groundHit;
            bool rayGrounded = Physics.Raycast(transform.position + Vector3.up * 0.35f, Vector3.down, out groundHit, 0.85f);
            bool isGroundedCheck = (controller != null && controller.isGrounded) || rayGrounded || (ladderExitGraceTimer > 0f);
            if (!isGroundedCheck)
            {
                verticalVelocity -= gravity * Time.deltaTime;
                if (verticalVelocity <= 0f && isJumping)
                {
                    // Apex reached, descending
                }
            }
            else
            {
                if (verticalVelocity <= 0f)
                {
                    isJumping = false;
                    verticalVelocity = -1.5f;
                }
            }

            // Check Track Zones
            bool wasClimbing = isClimbingLadder;
            isClimbingLadder = TrackPath.Instance != null && TrackPath.Instance.IsLadder(currentTrackDistance);
            bool inSlider = TrackPath.Instance != null && TrackPath.Instance.IsSlider(currentTrackDistance);

            if (wasClimbing && !isClimbingLadder)
            {
                verticalVelocity = -2.0f;
                isJumping = false;
                ladderExitGraceTimer = 0.55f;

                if (TrackPath.Instance != null)
                {
                    PathPoint ptExit = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
                    Vector3 snapPos = ptExit.position + (ptExit.right * lateralOffset);
                    snapPos.y = ptExit.position.y;

                    bool ccEnabled = controller != null && controller.enabled;
                    if (controller != null) controller.enabled = false;
                    transform.position = snapPos;
                    if (ptExit.forward != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(ptExit.forward, Vector3.up);
                    }
                    if (controller != null) controller.enabled = ccEnabled;
                }
            }

            if (inSlider != isSliderChute)
            {
                isSliderChute = inSlider;
            }

            if (isClimbingLadder)
            {
                verticalVelocity = 0f;
                isJumping = false;

                // Autonomous AI timing for rotating obstacle on ladder
                isHoldingLadder = false;
                LadderRotatingObstacle[] ladderObs = Object.FindObjectsByType<LadderRotatingObstacle>();
                for (int o = 0; o < ladderObs.Length; o++)
                {
                    float dy = ladderObs[o].transform.position.y - transform.position.y;
                    // If obstacle is just above the AI (between 0.35m and 2.6m above) and unsafe to pass:
                    if (dy > 0.30f && dy < 2.6f && !ladderObs[o].IsSafeToPass())
                    {
                        if (Random.value > mistakeChance)
                        {
                            isHoldingLadder = true;
                            break;
                        }
                    }
                }

                if (!isHoldingLadder)
                {
                    currentTrackDistance += CurrentSpeed * 0.74f * Time.deltaTime;
                }

                lateralOffset = Mathf.Clamp(lateralOffset, -0.7f, 0.7f);
                PathPoint ptLadder = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
                Vector3 targetFloorPosLadder = ptLadder.position + (ptLadder.right * (lateralOffset * 0.4f)) + (ptLadder.up * 0.25f);
                Vector3 posDiffLadder = targetFloorPosLadder - transform.position;
                controller.Move(posDiffLadder * 16f * Time.deltaTime);

                Vector3 ladderFacing = -ptLadder.up;
                ladderFacing.y = 0f;
                if (ladderFacing.sqrMagnitude < 0.01f) ladderFacing = ptLadder.forward;
                ladderFacing.y = 0f;
                if (ladderFacing.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotLadder = Quaternion.LookRotation(ladderFacing.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotLadder, Time.deltaTime * 18f);
                }

                HandleVisuals();
                return;
            }

            // Detect moving platform underneath and ground slope
            Vector3 platformMotion = Vector3.zero;
            bool onGround = rayGrounded;
            float forwardSlope = 0f;

            if (onGround)
            {
                MovingPlatform mp = groundHit.collider.GetComponentInParent<MovingPlatform>();
                if (mp != null)
                {
                    platformMotion = mp.Velocity * Time.deltaTime;
                    if (Mathf.Abs(mp.AngularVelocityY) > 0.1f)
                    {
                        transform.Rotate(Vector3.up, mp.AngularVelocityY * Time.deltaTime, Space.World);
                    }
                }

                forwardSlope = Vector3.Dot(groundHit.normal, transform.forward);
            }
            else if (TrackPath.Instance != null)
            {
                PathPoint ptNow = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
                forwardSlope = -ptNow.forward.y;
            }

            bool isClimbingSlope = forwardSlope < -0.10f;
            bool isDownhillSlope = forwardSlope > 0.10f;

            if (!isWaitingForObstacle)
            {
                float speedMult = 1.0f;
                if (isSliderChute)
                {
                    speedMult = 1.65f;
                }
                else if (isSliding)
                {
                    speedMult = isDownhillSlope ? 1.45f : 1.2f;
                }
                else if (isDownhillSlope)
                {
                    speedMult = 1.15f;
                }
                else if (isClimbingSlope)
                {
                    speedMult = 0.88f;
                }

                currentTrackDistance += CurrentSpeed * speedMult * Time.deltaTime;
            }

            if (ladderExitGraceTimer > 0f)
            {
                // Smoothly guide AI racer toward default lane when stepping off ladder
                lateralOffset = Mathf.MoveTowards(lateralOffset, defaultLateralOffset, Time.deltaTime * 2.5f);
            }

            if (TrackPath.Instance != null)
            {
                PathPoint pt = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
                Vector3 targetFloorPos = pt.position + (pt.right * lateralOffset);

                Vector3 currentPos = transform.position;
                Vector3 horizontalDiff = new Vector3(targetFloorPos.x - currentPos.x, 0f, targetFloorPos.z - currentPos.z);

                // Vertical elevation tracking: smoothly follows rising crests and snaps snugly to dips
                // CRITICAL: NEVER override upward jump velocity! Only snap to ground when NOT jumping and grounded.
                float yTargetDiff = targetFloorPos.y - currentPos.y;
                float verticalMove = verticalVelocity;
                if (!isJumping && isGroundedCheck && verticalVelocity <= 0f)
                {
                    if (ladderExitGraceTimer > 0f)
                    {
                        // Secure ground lock onto top deck during exit transition
                        verticalMove = (yTargetDiff > 0f) ? (yTargetDiff * 18f) : -2.5f;
                    }
                    else if (yTargetDiff > 0.02f)
                    {
                        verticalMove = Mathf.Max(verticalVelocity, yTargetDiff * 14f);
                    }
                    else
                    {
                        verticalMove = Mathf.Min(verticalVelocity, -2.5f);
                    }
                }

                Vector3 motion = horizontalDiff * 12f + Vector3.up * verticalMove;
                controller.Move(motion * Time.deltaTime + platformMotion);

                if (pt.forward != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(pt.forward, pt.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 12f);
                }
            }
            else
            {
                Vector3 motion = new Vector3(0f, verticalVelocity, CurrentSpeed);
                controller.Move(motion * Time.deltaTime + platformMotion);
            }
        }

        private void HandleKnockoutFall()
        {
            knockoutVelocity.y -= gravity * 1.35f * Time.deltaTime;
            controller.Move(knockoutVelocity * Time.deltaTime);

            transform.Rotate(tumbleTorque * Time.deltaTime, Space.World);

            // Water hit: triggers when reaching the water surface at y <= -5.2m
            if (transform.position.y <= -5.2f && !hasHitWater)
            {
                TriggerWaterFall();
            }
        }

        public void OnWaterSurfaceReached(float waterY)
        {
            if (!hasHitWater)
            {
                TriggerWaterFall();
            }
        }

        private void TriggerWaterFall()
        {
            if (hasHitWater) return;
            hasHitWater = true;
            isFalling = true;
            isKnockedOut = true;

            // Track death location for mistake mitigation
            if (Mathf.Abs(currentTrackDistance - lastDeathDist) < 12f)
            {
                consecutiveDeathsAtSamePoint++;
            }
            else
            {
                consecutiveDeathsAtSamePoint = 1;
                lastDeathDist = currentTrackDistance;
            }

            Vector3 splashPos = new Vector3(transform.position.x, -5.5f, transform.position.z);
            if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnWaterSplash(splashPos);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySplashSound();

            SetCharacterMeshVisible(false);

            StartCoroutine(RespawnRoutine());
        }

        private void SetCharacterMeshVisible(bool visible)
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                cachedRenderers = GetComponentsInChildren<Renderer>();
            }
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null) cachedRenderers[i].enabled = visible;
            }
        }

        private IEnumerator RespawnRoutine()
        {
            // Wait for splash, ripples and audio
            yield return new WaitForSeconds(0.70f);

            SetCharacterMeshVisible(true);
            hasHitWater = false;
            controller.enabled = false;
            currentTrackDistance = lastCheckpointDist;
            verticalVelocity = 0f;
            isJumping = false;
            jumpCooldownTimer = 0.2f;
            ladderExitGraceTimer = 0f;

            if (TrackPath.Instance != null)
            {
                PathPoint pt = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
                transform.position = pt.position + (pt.right * lateralOffset) + Vector3.up * 0.25f;
                transform.rotation = Quaternion.LookRotation(pt.forward, Vector3.up);
            }
            else
            {
                transform.position = startPosition;
                transform.rotation = Quaternion.identity;
            }

            isKnockedOut = false;
            isFalling = false;
            isWaitingForObstacle = false;
            isClimbingLadder = false;
            isSliderChute = false;
            isHoldingLadder = false;
            knockoutVelocity = Vector3.zero;
            tumbleTorque = Vector3.zero;
            SetSliding(false);
            ResetLimbs();

            if (ProceduralFX.Instance != null)
            {
                ProceduralFX.Instance.SpawnCoinBurst(transform.position + Vector3.up * 0.5f);
            }

            controller.enabled = true;
        }

        public void ApplyShield()
        {
            hasShield = true;
            if (activeShieldVisual != null) Destroy(activeShieldVisual);
            activeShieldVisual = CreateShieldBubble(transform);
            if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnCoinBurst(transform.position + Vector3.up * 1f);
        }

        public void PopShield()
        {
            hasShield = false;
            if (activeShieldVisual != null)
            {
                Destroy(activeShieldVisual);
                activeShieldVisual = null;
            }
            if (ProceduralFX.Instance != null)
            {
                ProceduralFX.Instance.SpawnHitPuff(transform.position + Vector3.up * 1f);
            }
        }

        public void ApplySpeedBoost(float duration, float multiplier)
        {
            isBoosted = true;
            boostTimer = duration;
            boostMultiplier = multiplier;
            if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnCoinBurst(transform.position + Vector3.up * 0.5f);
        }

        private GameObject CreateShieldBubble(Transform parent)
        {
            GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "AIShieldBubble";
            bubble.transform.SetParent(parent, false);
            bubble.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            bubble.transform.localScale = new Vector3(1.75f, 1.95f, 1.75f);

            Collider col = bubble.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = bubble.GetComponent<Renderer>();
            if (r != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.15f, 0.85f, 1.0f, 0.45f);
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
                r.material = mat;
            }
            return bubble;
        }

        public void OnObstacleHit(Vector3 contactPoint, Vector3 impactVelocity, float pushForce = 6.5f)
        {
            if (isKnockedOut || isFalling) return;

            // 1. Shield absorbs hit completely!
            if (hasShield)
            {
                PopShield();
                if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnHitPuff(contactPoint + Vector3.up * 0.5f);
                stumbleTimer = 0.35f;
                return;
            }

            // 2. Glancing hit on grounded track with low push force -> stumble
            if (pushForce < 4.0f && controller.isGrounded && !isClimbingLadder)
            {
                stumbleTimer = 0.65f;
                if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnHitPuff(contactPoint + Vector3.up * 0.5f);
                currentTrackDistance = Mathf.Max(0f, currentTrackDistance - 0.7f);
                return;
            }

            // 3. Direct heavy hit: knockout & tumble
            isKnockedOut = true;
            isFalling = true;
            if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnHitPuff(contactPoint + Vector3.up * 0.5f);

            if (impactVelocity.sqrMagnitude > 0.1f)
            {
                knockoutVelocity = (impactVelocity * 0.95f) + (-transform.forward * 3.0f) + (Vector3.up * 4.8f);
            }
            else
            {
                Vector3 backDir = -transform.forward;
                float sideSign = (transform.position.x > contactPoint.x) ? 1.0f : -1.0f;
                knockoutVelocity = (backDir * 4.5f) + (transform.right * sideSign * 4.2f) + (Vector3.up * 5.0f);
            }

            float rollDir = (contactPoint.x > transform.position.x) ? -180f : 180f;
            tumbleTorque = new Vector3(180f, rollDir, 75f);
        }

        public void OnObstacleHit(Vector3 contactPoint)
        {
            OnObstacleHit(contactPoint, Vector3.zero, 6.5f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isKnockedOut || isFalling) return;

            LadderSweeperHazard ladderHazard = other.GetComponent<LadderSweeperHazard>();
            if (ladderHazard != null && ladderHazard.parentObstacle != null && ladderHazard.parentObstacle.CanDamage && !ladderHazard.parentObstacle.IsSafeToPass())
            {
                Vector3 contact = other.ClosestPoint(transform.position);
                Vector3 impactVel = ladderHazard.parentObstacle.CalculateImpactVelocity(contact, transform);
                OnObstacleHit(contact, impactVel, ladderHazard.parentObstacle.PushbackForce);
                return;
            }

            ObstacleBase obs = other.GetComponent<ObstacleBase>() ?? other.GetComponentInParent<ObstacleBase>();
            if (obs != null && obs.CanDamage)
            {
                Vector3 contact = other.ClosestPoint(transform.position);
                Vector3 impactVel = obs.CalculateImpactVelocity(contact, transform);
                OnObstacleHit(contact, impactVel, obs.PushbackForce);
                return;
            }

            LargeRingSegmentCollider ringSeg = other.GetComponent<LargeRingSegmentCollider>();
            if (ringSeg != null && ringSeg.parentRing != null && ringSeg.parentRing.CanDamage)
            {
                Vector3 contact = other.ClosestPoint(transform.position);
                Vector3 impactVel = ringSeg.parentRing.CalculateImpactVelocity(contact, transform);
                OnObstacleHit(contact, impactVel, ringSeg.parentRing.PushbackForce);
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (isKnockedOut || isFalling) return;

            LadderSweeperHazard ladderHazard = hit.collider.GetComponent<LadderSweeperHazard>();
            if (ladderHazard != null && ladderHazard.parentObstacle != null && ladderHazard.parentObstacle.CanDamage && !ladderHazard.parentObstacle.IsSafeToPass())
            {
                Vector3 impactVel = ladderHazard.parentObstacle.CalculateImpactVelocity(hit.point, transform);
                OnObstacleHit(hit.point, impactVel, ladderHazard.parentObstacle.PushbackForce);
                return;
            }

            ObstacleBase obs = hit.collider.GetComponent<ObstacleBase>() ?? hit.collider.GetComponentInParent<ObstacleBase>();
            if (obs != null && obs.CanDamage)
            {
                Vector3 impactVel = obs.CalculateImpactVelocity(hit.point, transform);
                OnObstacleHit(hit.point, impactVel, obs.PushbackForce);
            }
        }

        private void AnimateStumble()
        {
            float stumbleProgress = stumbleTimer > 0f ? Mathf.Clamp01(1f - (stumbleTimer / 0.65f)) : 1f;
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

        private void HandleVisuals()
        {
            if (stumbleTimer > 0f)
            {
                AnimateStumble();
                return;
            }

            // Jumping animation (dynamic 3-phase)
            if (isJumping || (!controller.isGrounded && Mathf.Abs(verticalVelocity) > 0.8f))
            {
                if (verticalVelocity > 1.6f)
                {
                    // Ascent
                    float tAscent = Mathf.Clamp01(verticalVelocity / 8.0f);
                    float jumpLegAngle = Mathf.Lerp(25f, 48f, tAscent);
                    if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(jumpLegAngle, 0f, 0f);
                    if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(jumpLegAngle + 3f, 0f, 0f);
                    if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-80f, 0f, -24f);
                    if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-80f, 0f, 24f);
                    if (torso != null)
                    {
                        torso.localPosition = initialTorsoPos + Vector3.up * 0.10f;
                        torso.localRotation = Quaternion.Euler(-7f, 0f, 0f);
                    }
                }
                else if (verticalVelocity > -1.6f)
                {
                    // Apex
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
                    // Descent
                    float tDescent = Mathf.Clamp01(-verticalVelocity / 12.0f);
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
                if (head != null) head.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                return;
            }

            if (isSliderChute)
            {
                AnimateSliderChute();
                return;
            }

            if (isClimbingLadder)
            {
                if (isHoldingLadder) AnimateClimbHold();
                else AnimateClimb();
                return;
            }

            // Sliding / Crawl low posture
            if (isSliding)
            {
                float t = Time.time * 18f;
                if (torso != null)
                {
                    torso.localPosition = initialTorsoPos + new Vector3(0f, -0.38f, 0.22f);
                    torso.localRotation = Quaternion.Euler(55f, 0f, 0f);
                }
                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-80f + Mathf.Sin(t) * 15f, 0f, -12f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-80f - Mathf.Sin(t) * 15f, 0f, 12f);
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(75f - Mathf.Sin(t) * 20f, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(75f + Mathf.Sin(t) * 20f, 0f, 0f);
                if (head != null) head.localRotation = Quaternion.Euler(20f, 0f, 0f);
                return;
            }

            // Climbing ladder / steep upward incline
            if (forwardSlope < -0.12f && CurrentSpeed > 0.1f)
            {
                AnimateClimb();
                return;
            }

            // Idle or paused waiting for obstacle
            if (CurrentSpeed <= 0.1f)
            {
                ResetLimbs();
                if (torso != null) torso.localPosition = initialTorsoPos + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.02f);
                return;
            }

            // Normal running cycle
            animTime += Time.deltaTime * 14f;

            float legSin = Mathf.Sin(animTime);
            float legAngle = legSin * 38f;
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(legAngle, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-legAngle, 0f, 0f);

            if (leftFoot != null) leftFoot.localRotation = Quaternion.Euler(Mathf.Clamp(-legAngle * 0.5f, -18f, 18f), 0f, 0f);
            if (rightFoot != null) rightFoot.localRotation = Quaternion.Euler(Mathf.Clamp(legAngle * 0.5f, -18f, 18f), 0f, 0f);

            float armAngle = legSin * 34f;
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-armAngle, 0f, 0f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(armAngle, 0f, 0f);

            if (torso != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(animTime)) * 0.06f;
                torso.localPosition = initialTorsoPos + Vector3.up * bounce;
                torso.localRotation = Quaternion.Euler(6f, 0f, 0f);
            }

            if (head != null)
            {
                head.localRotation = Quaternion.Euler(Mathf.Sin(animTime * 2f) * 4f, 0f, 0f);
            }
        }

        private void AnimateClimb()
        {
            animTime += Time.deltaTime * 16f;
            float legSin = Mathf.Sin(animTime);
            float legAngle = legSin * 48f;

            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(legAngle, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-legAngle, 0f, 0f);

            float armAngle = legSin * 46f;
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-armAngle - 25f, 0f, -10f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(armAngle - 25f, 0f, 10f);

            if (torso != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(animTime)) * 0.08f;
                torso.localPosition = initialTorsoPos + Vector3.up * bounce + new Vector3(0f, -0.08f, 0.12f);
                torso.localRotation = Quaternion.Euler(24f, 0f, 0f);
            }
            if (head != null) head.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        }

        private void AnimateClimbHold()
        {
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
            float jitter = Mathf.Sin(Time.time * 20f) * 0.02f;
            if (torso != null)
            {
                torso.localPosition = initialTorsoPos + new Vector3(0f, -0.38f + jitter, -0.05f);
                torso.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            }
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(-75f, -6f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-75f, 6f, 0f);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(30f, 0f, 28f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(30f, 0f, -28f);
            if (head != null) head.localRotation = Quaternion.Euler(10f, 0f, 0f);
        }

        private void AnimateVictoryDance()
        {
            float t = Time.time * 6f;
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-145f, 0f, -20f + Mathf.Sin(t) * 12f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-145f, 0f, 20f - Mathf.Sin(t) * 12f);
            if (torso != null)
            {
                torso.localPosition = initialTorsoPos + Vector3.up * Mathf.Max(0f, Mathf.Sin(t * 1.5f) * 0.16f);
                torso.localRotation = Quaternion.identity;
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
            if (torso != null)
            {
                torso.localPosition = initialTorsoPos;
                torso.localRotation = Quaternion.identity;
            }
        }

        public void SetCheckpoint(Vector3 pos, float dist)
        {
            lastCheckpointDist = dist;
            consecutiveDeathsAtSamePoint = 0;
        }

        public void PlaceOnPodium(Vector3 podiumPos, Quaternion podiumRot)
        {
            controller.enabled = false;
            transform.position = podiumPos;
            transform.rotation = podiumRot;
            isCelebration = true;
        }

        public void ResetRacer(Vector3 spawnPos, float startDist = 0f)
        {
            StopAllCoroutines();
            controller.enabled = false;
            transform.position = spawnPos;
            startPosition = spawnPos;
            currentTrackDistance = startDist;
            lastCheckpointDist = startDist;
            transform.rotation = Quaternion.identity;
            verticalVelocity = 0f;
            isJumping = false;
            isKnockedOut = false;
            isFalling = false;
            isCelebration = false;
            isWaitingForObstacle = false;
            consecutiveDeathsAtSamePoint = 0;
            SetSliding(false);
            ResetLimbs();
            controller.enabled = true;
        }

        public void Configure(string name, Color color, float speed, float defaultLateral,
            Transform torsoT, Transform lLeg, Transform rLeg, Transform lArm, Transform rArm,
            Transform lFoot = null, Transform rFoot = null, Transform lHand = null, Transform rHand = null,
            Transform headT = null)
        {
            racerName = name;
            racerColor = color;
            baseSpeed = speed;
            defaultLateralOffset = defaultLateral;
            lateralOffset = defaultLateral;
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
