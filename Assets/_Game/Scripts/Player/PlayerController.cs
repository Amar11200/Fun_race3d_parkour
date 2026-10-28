using System.Collections;
using UnityEngine;

namespace DashMasters
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Track Following & Speed")]
        [SerializeField] private float baseSpeed = 7.5f;
        [SerializeField] private float lateralSpeed = 6.0f;
        [SerializeField] private float trackHalfWidth = 1.8f;
        [SerializeField] private bool holdToRun = true;

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpHeight = 2.0f;
        [SerializeField] private float gravity = 26f;

        [Header("Slide / Crawl Settings")]
        [SerializeField] private float slideHeight = 0.55f;

        [Header("Boost Settings")]
        [SerializeField] private float boostMultiplier = 1.5f;

        [Header("Visual References")]
        [SerializeField] private Transform characterModel;
        [SerializeField] private TrailRenderer boostTrail;

        private CharacterController controller;
        private PlayerVisuals visuals;

        private float currentTrackDistance = 0f;
        private float lateralOffset = -0.35f; // Player start lane
        private float verticalVelocity = 0f;
        private float originalHeight;
        private Vector3 originalCenter;

        private bool isSliding = false;
        private bool isClimbingLadder = false;
        private bool isSliderChute = false;
        private bool isBoosted = false;
        private float boostTimer = 0f;
        private bool hasShield = false;
        private GameObject activeShieldVisual;
        private float stumblePenaltyTimer = 0f;
        private float lateralVelocity = 0f;
        private float lastLateralOffset = 0f;
        private bool isKnockedOut = false;
        private bool isFalling = false;
        private bool hasHitWater = false;
        private Vector3 knockoutVelocity;
        private Vector3 tumbleTorque;
        private Renderer[] cachedRenderers;

        private bool isJumping = false;
        private float lastGroundedTime = 0f;
        private float jumpBufferTimer = 0f;
        private float jumpCooldownTimer = 0f;
        private float ladderExitGraceTimer = 0f;
        private const float JUMP_BUFFER_TIME = 0.18f;
        private const float COYOTE_TIME = 0.15f;

        private float lastCheckpointDist = 0f;
        private Vector3 lastCheckpointPos;
        private Quaternion lastCheckpointRot;

        public bool HoldToRun { get => holdToRun; set => holdToRun = value; }
        public float BaseSpeed { get => baseSpeed; set => baseSpeed = value; }
        public float JumpHeight { get => jumpHeight; set => jumpHeight = value; }
        public float Gravity { get => gravity; set => gravity = value; }
        public bool IsMovingForward { get; private set; }
        public bool IsBoosted => isBoosted;
        public bool HasShield => hasShield;
        public float VerticalVelocity => verticalVelocity;
        public float LateralVelocity => lateralVelocity;
        public float CurrentSpeed
        {
            get
            {
                float spd = isBoosted ? baseSpeed * boostMultiplier : baseSpeed;
                if (stumblePenaltyTimer > 0f) spd *= 0.55f;
                return spd;
            }
        }
        public float CurrentTrackDistance => currentTrackDistance;
        public bool IsClimbingLadder => isClimbingLadder;
        public bool IsGrounded => !isJumping && ((controller != null && controller.isGrounded) || (Time.time - lastGroundedTime < 0.05f));

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            originalHeight = controller.height;
            originalCenter = controller.center;
            lastCheckpointPos = transform.position;
            lastCheckpointRot = transform.rotation;
            visuals = GetComponentInChildren<PlayerVisuals>();
        }

        private void Start()
        {
            if (boostTrail != null) boostTrail.emitting = false;
        }

        private void Update()
        {
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

            // Buffer Jump Input so Spacebar presses are reliably detected
            if (ReadJumpInput())
            {
                jumpBufferTimer = JUMP_BUFFER_TIME;
            }
            else if (jumpBufferTimer > 0f)
            {
                jumpBufferTimer -= Time.deltaTime;
            }

            if (jumpCooldownTimer > 0f)
            {
                jumpCooldownTimer -= Time.deltaTime;
            }

            HandleTimers();
            HandleTrackMovement();
            HandleVisuals();
        }

        private void ApplyGravityOnly()
        {
            if (controller.isGrounded)
            {
                verticalVelocity = -1f;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }
            controller.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);
        }

        private void HandleTimers()
        {
            if (stumblePenaltyTimer > 0f)
            {
                stumblePenaltyTimer -= Time.deltaTime;
            }

            lateralVelocity = (lateralOffset - lastLateralOffset) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastLateralOffset = lateralOffset;

            if (ladderExitGraceTimer > 0f)
            {
                ladderExitGraceTimer -= Time.deltaTime;
            }

            if (isBoosted)
            {
                boostTimer -= Time.deltaTime;
                if (boostTimer <= 0f)
                {
                    isBoosted = false;
                    if (boostTrail != null) boostTrail.emitting = false;
                }
            }
        }

        private void HandleTrackMovement()
        {
            bool wantsToRun = CheckRunHeld();
            IsMovingForward = wantsToRun;

            // Check Track Zones
            bool wasClimbing = isClimbingLadder;
            isClimbingLadder = TrackPath.Instance != null && TrackPath.Instance.IsLadder(currentTrackDistance);
            bool inSlider = TrackPath.Instance != null && TrackPath.Instance.IsSlider(currentTrackDistance);

            // Handle transition off ladder onto top deck
            if (wasClimbing && !isClimbingLadder)
            {
                verticalVelocity = -2.0f;
                lastGroundedTime = Time.time;
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

                if (visuals != null)
                {
                    visuals.SetClimbing(false);
                    visuals.SetClimbingHold(false);
                }
            }

            if (inSlider != isSliderChute)
            {
                isSliderChute = inSlider;
                if (visuals != null) visuals.SetSliderChute(isSliderChute);
            }

            if (isClimbingLadder)
            {
                // Ladder mechanics: holding onto ladder rungs, vertical climbing
                verticalVelocity = 0f;
                lastGroundedTime = Time.time;
                isJumping = false;

                if (wantsToRun)
                {
                    float climbSpeed = CurrentSpeed * 0.78f;
                    currentTrackDistance += climbSpeed * Time.deltaTime;
                    if (visuals != null)
                    {
                        visuals.SetClimbing(true);
                        visuals.SetClimbingHold(false);
                    }
                }
                else
                {
                    // Released W: hold in place on ladder rungs
                    if (visuals != null)
                    {
                        visuals.SetClimbing(true);
                        visuals.SetClimbingHold(true);
                    }
                }

                // Clamped lateral dodge on ladder rungs
                float hLadder = ReadHorizontalInput();
                lateralOffset += hLadder * (lateralSpeed * 0.5f) * Time.deltaTime;
                lateralOffset = Mathf.Clamp(lateralOffset, -0.9f, 0.9f);

                PathPoint ptLadder = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
                Vector3 targetFloorPosLadder = ptLadder.position + (ptLadder.right * lateralOffset) + (ptLadder.up * 0.25f);
                Vector3 posDiffLadder = targetFloorPosLadder - transform.position;
                controller.Move(posDiffLadder * 16f * Time.deltaTime);

                // Face the ladder wall rungs (chest facing wall), keeping upright
                Vector3 ladderFacing = -ptLadder.up;
                ladderFacing.y = 0f;
                if (ladderFacing.sqrMagnitude < 0.01f) ladderFacing = ptLadder.forward;
                ladderFacing.y = 0f;
                if (ladderFacing.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotLadder = Quaternion.LookRotation(ladderFacing.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotLadder, Time.deltaTime * 18f);
                }

                return;
            }

            // Slide / Crawl input (Hold S or Down Arrow)
            bool wantsSlide = ReadSlideInput();
            if (!isSliderChute && wantsSlide != isSliding)
            {
                SetSliding(wantsSlide);
            }

            // Ground & Platform detection
            Vector3 platformMotion = Vector3.zero;
            RaycastHit groundHit;
            bool rayGrounded = Physics.Raycast(transform.position + Vector3.up * 0.35f, Vector3.down, out groundHit, 0.85f);
            bool isGroundedCheck = (controller != null && controller.isGrounded) || rayGrounded || (ladderExitGraceTimer > 0f);

            if (isGroundedCheck && !isJumping)
            {
                lastGroundedTime = Time.time;
            }

            float forwardSlope = 0f;
            if (rayGrounded)
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

                // Slope climbing check (forward face angle)
                forwardSlope = Vector3.Dot(groundHit.normal, transform.forward);
            }
            else if (TrackPath.Instance != null)
            {
                PathPoint ptNow = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
                forwardSlope = -ptNow.forward.y;
            }

            bool isClimbingSlope = forwardSlope < -0.10f;
            bool isDownhillSlope = forwardSlope > 0.10f;

            if (visuals != null)
            {
                visuals.SetClimbing(isClimbingSlope && wantsToRun);
            }

            // Jump handling: Spacebar pressed while grounded and not sliding
            bool canJump = (Time.time - lastGroundedTime <= COYOTE_TIME || isGroundedCheck) && !isSliding && jumpCooldownTimer <= 0f;

            if (jumpBufferTimer > 0f && canJump)
            {
                isJumping = true;
                jumpBufferTimer = 0f;
                jumpCooldownTimer = 0.22f;
                lastGroundedTime = -10f; // Consume coyote time

                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * gravity);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayJumpSound();
                }
            }
            else if (isJumping)
            {
                verticalVelocity -= gravity * Time.deltaTime;

                // Landed check: descending and hitting ground surface
                if (verticalVelocity < 0f && (controller.isGrounded || (rayGrounded && groundHit.distance < 0.45f)))
                {
                    isJumping = false;
                    verticalVelocity = -1.5f;
                    lastGroundedTime = Time.time;
                }
            }
            else if (isGroundedCheck)
            {
                verticalVelocity = -1.5f;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            // Advance distance along track path if running (with slope physics & downhill slide rush)
            if (isSliderChute)
            {
                if (!isSliding) SetSliding(true);
                currentTrackDistance += CurrentSpeed * 1.68f * Time.deltaTime;
            }
            else if (wantsToRun)
            {
                float speedMultiplier = 1.0f;
                if (isSliding)
                {
                    // Sliding: exciting downhill momentum glide or horizontal slide
                    speedMultiplier = isDownhillSlope ? 1.55f : 1.25f;
                }
                else if (isDownhillSlope)
                {
                    speedMultiplier = 1.18f; // Natural downhill gravity assist
                }
                else if (isClimbingSlope)
                {
                    speedMultiplier = 0.88f; // Steady climbing exertion
                }

                currentTrackDistance += CurrentSpeed * speedMultiplier * Time.deltaTime;
            }

            // Optional lateral dodge (A/D or mouse swipe)
            float h = ReadHorizontalInput();
            if (ladderExitGraceTimer > 0f)
            {
                // Smoothly guide racer toward track center when stepping off ladder
                lateralOffset = Mathf.MoveTowards(lateralOffset, 0f, Time.deltaTime * 2.5f);
            }
            lateralOffset += h * lateralSpeed * Time.deltaTime;
            lateralOffset = Mathf.Clamp(lateralOffset, -trackHalfWidth, trackHalfWidth);

            // Evaluate 3D curve from TrackPath
            if (TrackPath.Instance != null)
            {
                PathPoint pt = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
                Vector3 targetFloorPos = pt.position + (pt.right * lateralOffset);

                // Compute desired move to stay locked to path while allowing vertical jump/fall
                Vector3 currentPos = transform.position;
                Vector3 horizontalDiff = new Vector3(targetFloorPos.x - currentPos.x, 0f, targetFloorPos.z - currentPos.z);

                // Vertical elevation tracking:
                float yTargetDiff = targetFloorPos.y - currentPos.y;
                float verticalMove = verticalVelocity;

                // CRITICAL: NEVER override upward jump velocity! Only snap to ground when NOT jumping and grounded.
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

                Vector3 motion = horizontalDiff * 14f + Vector3.up * verticalMove;
                controller.Move(motion * Time.deltaTime + platformMotion);

                // Smoothly orient along path forward tangent AND tilt with slope
                if (pt.forward != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(pt.forward, pt.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 14f);
                }
            }
            else
            {
                Vector3 motion = new Vector3(h * lateralSpeed, verticalVelocity, wantsToRun ? CurrentSpeed : 0f);
                controller.Move(motion * Time.deltaTime + platformMotion);
            }

            // Check if racer fell off exposed track into open air (never during ladder climb or exit grace period)
            if (transform.position.y < -1.8f && !isGroundedCheck && !isKnockedOut && !isClimbingLadder && ladderExitGraceTimer <= 0f)
            {
                isKnockedOut = true;
                knockoutVelocity = (transform.forward * (wantsToRun ? CurrentSpeed : 2.5f) * 0.45f) + (Vector3.down * 3.2f);
                tumbleTorque = new Vector3(85f, 65f, 40f);
                if (visuals != null) visuals.SetFalling(true);
            }
        }

        private void SetSliding(bool sliding)
        {
            isSliding = sliding;
            if (sliding)
            {
                controller.height = slideHeight;
                controller.center = new Vector3(originalCenter.x, slideHeight * 0.5f, originalCenter.z);
                if (visuals != null) visuals.SetSliding(true);
            }
            else
            {
                controller.height = originalHeight;
                controller.center = originalCenter;
                if (visuals != null) visuals.SetSliding(false);
            }
        }

        private void HandleKnockoutFall()
        {
            // Apply physics arc tumble toward water
            knockoutVelocity.y -= gravity * 1.35f * Time.deltaTime;
            controller.Move(knockoutVelocity * Time.deltaTime);

            // Realistic rotational torque
            transform.Rotate(tumbleTorque * Time.deltaTime, Space.World);

            // Water hit: only triggers when character actually reaches the water surface at y = -5.2m
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
            IsMovingForward = false;

            Vector3 splashPos = new Vector3(transform.position.x, -5.5f, transform.position.z);
            if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnWaterSplash(splashPos);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySplashSound();
            if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.TriggerShake(0.55f, 0.45f);

            // Submerge character mesh below waterline
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
            // Wait for water splash, particles and sound to play
            yield return new WaitForSeconds(0.70f);

            // Re-enable character mesh
            SetCharacterMeshVisible(true);

            // Reset to checkpoint
            controller.enabled = false;
            currentTrackDistance = lastCheckpointDist;
            lateralOffset = -0.35f;
            verticalVelocity = 0f;

            if (TrackPath.Instance != null)
            {
                PathPoint pt = TrackPath.Instance.EvaluateDistance(currentTrackDistance);
                transform.position = pt.position + (pt.right * lateralOffset) + Vector3.up * 0.25f;
                transform.rotation = Quaternion.LookRotation(pt.forward, Vector3.up);
            }
            else
            {
                transform.position = lastCheckpointPos;
                transform.rotation = lastCheckpointRot;
            }

            SetSliding(false);
            isClimbingLadder = false;
            isSliderChute = false;
            isJumping = false;
            jumpBufferTimer = 0f;
            jumpCooldownTimer = 0f;
            ladderExitGraceTimer = 0f;
            stumblePenaltyTimer = 0f;
            lastGroundedTime = Time.time;
            isKnockedOut = false;
            isFalling = false;
            hasHitWater = false;
            knockoutVelocity = Vector3.zero;
            tumbleTorque = Vector3.zero;
            if (visuals != null)
            {
                visuals.SetFalling(false);
                visuals.SetKnockout(false);
                visuals.SetStumble(false);
            }

            if (ProceduralFX.Instance != null)
            {
                ProceduralFX.Instance.SpawnCoinBurst(transform.position + Vector3.up * 0.6f);
            }

            controller.enabled = true;
        }

        public void ApplyShield()
        {
            hasShield = true;
            if (activeShieldVisual != null) Destroy(activeShieldVisual);
            activeShieldVisual = CreateShieldBubble(transform);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBoostSound();
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

        private GameObject CreateShieldBubble(Transform parent)
        {
            GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "ActiveShieldBubble";
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

        public void SetCostumeMaterials(Material bodyMat, Material accentMat)
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                cachedRenderers = GetComponentsInChildren<Renderer>();
            }
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] == null) continue;
                if (cachedRenderers[i].gameObject.name.Contains("Shield") || cachedRenderers[i] is TrailRenderer) continue;

                string n = cachedRenderers[i].gameObject.name.ToLower();
                if (n.Contains("head") || n.Contains("joint") || n.Contains("hand") || n.Contains("foot"))
                {
                    if (accentMat != null) cachedRenderers[i].material = accentMat;
                }
                else
                {
                    if (bodyMat != null) cachedRenderers[i].material = bodyMat;
                }
            }
        }

        public void OnObstacleHit(Vector3 contactPoint, Vector3 impactVelocity, float pushForce = 6.5f)
        {
            // Debounce: prevent multiple triggers during one collision
            if (isKnockedOut || isFalling) return;

            // 1. SHIELD PROTECTION: Absorbs one dangerous obstacle hit completely!
            if (hasShield)
            {
                PopShield();
                if (AudioManager.Instance != null) AudioManager.Instance.PlayHitSound();
                if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnHitPuff(contactPoint + Vector3.up * 0.5f);
                if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.TriggerShake(0.35f, 0.25f);
                if (visuals != null) visuals.SetStumble(true, 0.25f);
                stumblePenaltyTimer = 0.35f;
                return;
            }

            // 2. GLANCING HIT vs DIRECT HEAVY HIT
            // If glancing impact on grounded track with lower force, stumble and lose speed instead of launching into water
            if (pushForce < 4.0f && IsGrounded && !isClimbingLadder)
            {
                stumblePenaltyTimer = 0.65f;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayHitSound();
                if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnHitPuff(contactPoint + Vector3.up * 0.5f);
                if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.TriggerShake(0.38f, 0.3f);
                if (visuals != null) visuals.SetStumble(true, 0.45f);
                currentTrackDistance = Mathf.Max(0f, currentTrackDistance - 0.75f);
                return;
            }

            // 3. DIRECT HEAVY HIT: Knockout trajectory with believable directional knockback
            isKnockedOut = true;
            isFalling = true;
            IsMovingForward = false;

            if (AudioManager.Instance != null) AudioManager.Instance.PlayHitSound();
            if (ProceduralFX.Instance != null) ProceduralFX.Instance.SpawnHitPuff(contactPoint + Vector3.up * 0.5f);
            if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.TriggerShake(0.6f, 0.5f);
            if (visuals != null)
            {
                visuals.SetStumble(true, 0.25f);
                visuals.SetFalling(true);
            }

            // Calculate realistic physical knockback velocity from impact vector
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

            // Rotational tumble torque based on contact
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

        private bool CheckRunHeld()
        {
            if (!holdToRun) return true;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            if (mouse != null && mouse.leftButton.isPressed) return true;
            if (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed)) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                {
                    return true;
                }
            }
            catch {}
#endif
            return false;
        }

        private float ReadHorizontalInput()
        {
            float h = 0f;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            }

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                float mouseX = (mouse.position.ReadValue().x / Screen.width) * 2f - 1f;
                if (Mathf.Abs(mouseX) > 0.15f)
                {
                    h = Mathf.Clamp(h + mouseX * 1.5f, -1f, 1f);
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                h += Input.GetAxisRaw("Horizontal");
                if (Input.GetMouseButton(0))
                {
                    float mouseX = (Input.mousePosition.x / Screen.width) * 2f - 1f;
                    if (Mathf.Abs(mouseX) > 0.15f)
                    {
                        h = Mathf.Clamp(h + mouseX * 1.5f, -1f, 1f);
                    }
                }
            }
            catch {}
#endif
            return Mathf.Clamp(h, -1f, 1f);
        }

        private bool ReadJumpInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space)) return true;
            }
            catch {}
#endif
            return false;
        }

        private bool ReadSlideInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed || kb.cKey.isPressed)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.C)) return true;
            }
            catch {}
#endif
            return false;
        }

        private void HandleVisuals()
        {
            if (characterModel == null) return;

            float targetTilt = -ReadHorizontalInput() * 12f;
            float targetPitch = isSliding ? 20f : 0f;
            float targetScaleY = isSliding ? 0.6f : (controller.isGrounded ? 1.0f : 1.1f);

            Quaternion targetRot = Quaternion.Euler(targetPitch, 0f, targetTilt);
            characterModel.localRotation = Quaternion.Lerp(characterModel.localRotation, targetRot, Time.deltaTime * 12f);
            characterModel.localScale = Vector3.Lerp(characterModel.localScale, new Vector3(1f, targetScaleY, 1f), Time.deltaTime * 10f);
        }

        public void ApplySpeedBoost(float duration, float multiplier)
        {
            isBoosted = true;
            boostTimer = duration;
            boostMultiplier = multiplier;

            if (boostTrail != null) boostTrail.emitting = true;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBoostSound();
            if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.TriggerBoostFov(1.2f, duration);
        }

        public void SetCheckpoint(Vector3 position, Quaternion rotation, float dist)
        {
            lastCheckpointPos = position;
            lastCheckpointRot = rotation;
            lastCheckpointDist = dist;
        }

        public void PlaceOnPodium(Vector3 podiumPos, Quaternion podiumRot)
        {
            controller.enabled = false;
            transform.position = podiumPos;
            transform.rotation = podiumRot;
            IsMovingForward = false;
            isBoosted = false;
            if (boostTrail != null) boostTrail.emitting = false;
            if (visuals != null) visuals.SetCelebration(true);
        }

        public void ResetRacer(Vector3 spawnPosition)
        {
            StopAllCoroutines();
            controller.enabled = false;
            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;
            lastCheckpointPos = spawnPosition;
            lastCheckpointRot = Quaternion.identity;
            currentTrackDistance = 0f;
            lastCheckpointDist = 0f;
            lateralOffset = -0.35f;
            verticalVelocity = 0f;
            isJumping = false;
            jumpBufferTimer = 0f;
            jumpCooldownTimer = 0f;
            ladderExitGraceTimer = 0f;
            lastGroundedTime = Time.time;
            isBoosted = false;
            isSliding = false;
            isKnockedOut = false;
            isFalling = false;
            IsMovingForward = false;
            if (boostTrail != null) boostTrail.emitting = false;
            if (visuals != null)
            {
                visuals.SetCelebration(false);
                visuals.SetFalling(false);
                visuals.SetKnockout(false);
                visuals.SetSliding(false);
                visuals.SetStumble(false);
            }
            SetSliding(false);
            controller.enabled = true;
        }
    }
}
