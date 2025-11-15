using UnityEngine;

namespace PassTrainer
{
    public class PassTrainerEnv : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private PassAgent passAgent;
        [SerializeField] private Rigidbody ballRigidbody;
        [SerializeField] private Transform launcher;
        [SerializeField] private Transform targetZone;
        [SerializeField] private bool autoCreateDefaults = true;

        [Header("Court Settings")]
        [SerializeField] private float courtHalfLength = 8f;
        [SerializeField] private float courtHalfWidth = 4f;
        [SerializeField] private Vector3 agentSpawn = new Vector3(-6f, 1f, 0f);

        [Header("Launch Settings")]
        [SerializeField] private float launchSpeed = 8f;
        [SerializeField] private float launchArcY = 0.4f;

        [Header("Hit Settings")]
        [SerializeField] private float hitRadius = 1f;
        [SerializeField] private float hitMaxHeight = 3f;
        [SerializeField] private float passSpeed = 6f;
        [SerializeField] private float targetRadius = 2f;

        [Header("Court Landmarks")]
        [SerializeField] private Transform netCenter;
        [SerializeField] private float netHeight = 2.43f;
        [SerializeField] private Transform passTargetCenter;
        [SerializeField] private Vector2 passTargetSize = new Vector2(2f, 2f);
        [SerializeField] private Transform teammateReserveCenter;
        [SerializeField] private Vector2 teammateReserveSize = new Vector2(2f, 2f);

        [Header("Rewards")]
        [SerializeField] private float contactBonus = 0.1f;
        [SerializeField] private float successReward = 1.0f;
        [SerializeField] private float shapingScale = 0.5f;
        [SerializeField] private float maxDistForShaping = 6f;
        [SerializeField] private float noTouchPenalty = -0.2f;
        [SerializeField] private float aliveReward = 0.005f;
        [SerializeField] private float aliveHeight = 0.75f;
        [SerializeField] private float spacingPenalty = -0.001f;
        [SerializeField] private float nearNetPenalty = -0.075f;
        [SerializeField] private float unplayablePenalty = -0.1f;
        [SerializeField] private float passBonusMin = 0.2f;
        [SerializeField] private float passBonusMax = 0.8f;
        [SerializeField] private float apexMargin = 0.3f;
        [SerializeField] private float inBoundsGroundPenalty = -1f;
        [SerializeField] private float outOfBoundsPenalty = -0.5f;
        [SerializeField] private float nearNetBuffer = 0.75f;

        private bool _episodeActive;
        private bool _touchedBall;
        private float _bestTargetError;
        private int _touchCount;

        private const float OutOfBoundsGrace = 1.5f;

        public PassAgent PassAgent => passAgent;
        public Rigidbody BallRigidbody => ballRigidbody;
        public Transform Launcher => launcher;
        public Transform TargetZone => targetZone;
        public Vector3 BallPosition => ballRigidbody != null ? ballRigidbody.position : Vector3.zero;
        public float HitRadius => hitRadius;
        public float HitMaxHeight => hitMaxHeight;
        public Vector3 BallVelocity => ballRigidbody != null ? ballRigidbody.velocity : Vector3.zero;
        public Vector3 TargetPosition => targetZone != null ? targetZone.position : Vector3.zero;
        public Vector3 NetPosition => netCenter != null ? netCenter.position : Vector3.zero;
        public Vector3 PassTargetPosition => (passTargetCenter != null ? passTargetCenter : targetZone) != null
            ? (passTargetCenter != null ? passTargetCenter.position : targetZone.position)
            : Vector3.zero;
        public Vector2 PassTargetExtents => passTargetSize;
        public Vector3 TeammateReservePosition => teammateReserveCenter != null ? teammateReserveCenter.position : Vector3.zero;
        public Vector2 TeammateReserveExtents => teammateReserveSize;
        public bool HasTeammateReserve => teammateReserveCenter != null;
        public float NetHeight => netHeight;
        public float CourtHalfLength => courtHalfLength;
        public float CourtHalfWidth => courtHalfWidth;
        public bool EpisodeActive => _episodeActive;
        public int TouchIndex => _touchCount;

        private void Awake()
        {
            TryAutoCreate();
            _touchCount = 0;
        }

        private void TryAutoCreate()
        {
            if (passAgent != null && ballRigidbody != null && launcher != null && targetZone != null)
            {
                //passAgent.SetEnvironment(this);
                return;
            }

            if (!autoCreateDefaults)
            {
                if (passAgent == null || ballRigidbody == null || launcher == null || targetZone == null)
                {
                    Debug.LogWarning($"{nameof(PassTrainerEnv)} missing references and autoCreateDefaults is false. Scene setup required.", this);
                }

                if (passAgent != null)
                {
                    //passAgent.SetEnvironment(this);
                }

                return;
            }

            CreateDebugEnvironment();
        }

        private void CreateDebugEnvironment()
        {
            if (passAgent == null)
            {
                var agentGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                agentGo.name = "PassAgent";
                agentGo.transform.SetParent(transform);
                agentGo.transform.position = agentSpawn;
                var agentRb = agentGo.GetComponent<Rigidbody>() ?? agentGo.AddComponent<Rigidbody>();
                agentRb.constraints = RigidbodyConstraints.FreezeRotation;
                passAgent = agentGo.AddComponent<PassAgent>();
                //passAgent.SetEnvironment(this);
            }

            if (ballRigidbody == null)
            {
                var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ballGo.name = "Ball";
                ballGo.transform.SetParent(transform);
                ballGo.transform.position = launcher != null ? launcher.position : agentSpawn + new Vector3(6f, 1.5f, 0f);
                ballRigidbody = ballGo.GetComponent<Rigidbody>() ?? ballGo.AddComponent<Rigidbody>();
                ballRigidbody.useGravity = true;
            }

            if (launcher == null)
            {
                var launcherGo = new GameObject("Launcher");
                launcherGo.transform.SetParent(transform);
                launcherGo.transform.position = agentSpawn + new Vector3(12f, 1.5f, 0f);
                launcher = launcherGo.transform;
            }

            if (targetZone == null)
            {
                var targetGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                targetGo.name = "TargetZone";
                targetGo.transform.SetParent(transform);
                targetGo.transform.position = agentSpawn + new Vector3(3.5f, -0.25f, 0f);
                targetGo.transform.localScale = new Vector3(targetRadius / 0.5f, 0.05f, targetRadius / 0.5f);
                targetZone = targetGo.transform;
                var targetCollider = targetGo.GetComponent<Collider>();
                if (targetCollider != null)
                {
                    Destroy(targetCollider);
                }
            }
        }

        public void BeginEpisode()
        {
            if (!EnsureReferences())
            {
                return;
            }

            _episodeActive = true;
            _touchedBall = false;
            _bestTargetError = float.MaxValue;
            _touchCount = 0;

            ResetAgent();
            ResetBall();
            LaunchBallOnce();
        }

        private bool EnsureReferences()
        {
            if (passAgent == null || ballRigidbody == null || launcher == null || targetZone == null)
            {
                Debug.LogWarning("PassTrainerEnv missing required references. Episode skipped.", this);
                return false;
            }

            //passAgent.SetEnvironment(this);
            return true;
        }

        private void ResetAgent()
        {
            if (passAgent == null)
                return;

            // Reset position & rotation
            passAgent.transform.position = agentSpawn;
            passAgent.transform.rotation = Quaternion.identity;

            // Reset physics
            var rb = passAgent.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void ResetBall()
        {
            var ballTransform = ballRigidbody.transform;
            ballTransform.position = launcher.position;
            ballTransform.rotation = Quaternion.identity;
            ballRigidbody.velocity = Vector3.zero;
            ballRigidbody.angularVelocity = Vector3.zero;
        }

        private void LaunchBallOnce()
        {
            Vector3 direction = (passAgent.transform.position - launcher.position);
            direction.y = Mathf.Max(direction.y, 0f) + launchArcY;
            Vector3 normalized = direction.normalized;
            ballRigidbody.velocity = normalized * launchSpeed;
        }

        public void TryHitBall(PassAgent agent)
        {
            if (!_episodeActive || agent != passAgent || ballRigidbody == null)
            {
                return;
            }

            Vector3 ballPos = ballRigidbody.position;
            Vector3 agentPos = passAgent.transform.position;

            var horizontalDelta = new Vector2(ballPos.x - agentPos.x, ballPos.z - agentPos.z);

            if (horizontalDelta.magnitude <= hitRadius && ballPos.y <= hitMaxHeight)
            {
                _touchedBall = true;
                _touchCount = Mathf.Min(_touchCount + 1, 2);
                Vector3 targetPos = targetZone != null ? targetZone.position : agentPos;
                Vector3 toTarget = targetPos - ballPos;
                toTarget.y = Mathf.Max(toTarget.y, 0f) + launchArcY;
                Vector3 passDir = toTarget.normalized;

                ballRigidbody.velocity = passDir * passSpeed;

                passAgent.AddReward(contactBonus);
                EvaluatePassQuality(ballPos, ballRigidbody.velocity);
            }
        }

        private void FixedUpdate()
        {
            if (!_episodeActive || ballRigidbody == null || targetZone == null)
            {
                return;
            }

            Vector3 ballPos = ballRigidbody.position;

            if (_touchedBall)
            {
                float targetError = Vector2.Distance(
                    new Vector2(ballPos.x, ballPos.z),
                    new Vector2(targetZone.position.x, targetZone.position.z));
                _bestTargetError = Mathf.Min(_bestTargetError, targetError);
            }

            if (IsBallCompletelyOut(ballPos))
            {
                OnBallDead(false, true, ballPos);
                return;
            }

            if (ballPos.y <= 0.5f && ballRigidbody.velocity.y <= 0f)
            {
                bool inTarget = IsInTargetZone(ballPos);
                bool outOfBounds = IsOutOfBounds(ballPos);
                OnBallDead(inTarget, outOfBounds, ballPos);
            }
        }

        private bool IsBallCompletelyOut(Vector3 pos)
        {
            return Mathf.Abs(pos.x) > courtHalfLength + OutOfBoundsGrace ||
                   Mathf.Abs(pos.z) > courtHalfWidth + OutOfBoundsGrace ||
                   pos.y < -1f || pos.y > 20f;
        }

        private bool IsInTargetZone(Vector3 position)
        {
            Vector2 delta = new Vector2(position.x - targetZone.position.x, position.z - targetZone.position.z);
            return delta.magnitude <= targetRadius;
        }

        private bool IsOutOfBounds(Vector3 position)
        {
            return Mathf.Abs(position.x) > courtHalfLength || Mathf.Abs(position.z) > courtHalfWidth;
        }

        private void OnBallDead(bool legacyTargetHit, bool outOfBounds, Vector3 landingPos)
        {
            if (!_episodeActive)
            {
                return;
            }

            _episodeActive = false;

            float reward = 0f;

            Transform targetRef = passTargetCenter != null ? passTargetCenter : targetZone;
            Vector2 targetSize = passTargetSize.sqrMagnitude > 0f ? passTargetSize : Vector2.one * (targetRadius * 2f);
            bool landedInTarget = targetRef != null && IsInsideBoxXZ(landingPos, targetRef, targetSize);

            if (landedInTarget || legacyTargetHit)
            {
                reward += successReward;
            }
            else if (_touchedBall && _bestTargetError < float.MaxValue)
            {
                float norm = Mathf.Clamp01(1f - (_bestTargetError / Mathf.Max(maxDistForShaping, 0.0001f)));
                reward += shapingScale * norm;
            }

            if (!_touchedBall)
            {
                reward += noTouchPenalty;
            }

            if (outOfBounds)
            {
                reward += outOfBoundsPenalty;
            }
            else
            {
                if (landingPos.y <= aliveHeight)
                {
                    reward += inBoundsGroundPenalty;
                }
            }

            passAgent.AddReward(reward);
            passAgent.EndEpisode();
        }

        /// <summary>
        /// Applies per-decision shaping rewards (spacing, alive bonus).
        /// </summary>
        public void ApplyStepRewards(PassAgent agent)
        {
            if (!_episodeActive || agent != passAgent)
            {
                return;
            }

            if (teammateReserveCenter != null &&
                IsInsideBoxXZ(agent.transform.position, teammateReserveCenter, teammateReserveSize))
            {
                agent.AddReward(spacingPenalty);
            }

            if (ballRigidbody != null && ballRigidbody.position.y > aliveHeight)
            {
                agent.AddReward(aliveReward);
            }
        }

        /// <summary>
        /// Checks if a world-space point lies within a rectangular box aligned to world axes.
        /// </summary>
        public bool IsInsideBoxXZ(Vector3 point, Transform center, Vector2 sizeXZ)
        {
            if (center == null)
            {
                return false;
            }

            Vector3 c = center.position;
            float halfX = sizeXZ.x * 0.5f;
            float halfZ = sizeXZ.y * 0.5f;
            return Mathf.Abs(point.x - c.x) <= halfX &&
                   Mathf.Abs(point.z - c.z) <= halfZ;
        }

        /// <summary>
        /// Predicts where the ball will land on the ground plane given current position and velocity.
        /// </summary>
        public Vector3 PredictLandingPoint(Vector3 position, Vector3 velocity)
        {
            float gravity = Physics.gravity.y;
            if (Mathf.Approximately(gravity, 0f))
            {
                return position;
            }

            float a = 0.5f * gravity;
            float b = velocity.y;
            float c = position.y;
            float discriminant = b * b - 4f * a * c;

            if (discriminant < 0f)
            {
                // Fallback: project a short time horizon.
                float fallbackTime = Mathf.Max(0.25f, -b / gravity);
                return position + velocity * fallbackTime + 0.5f * Physics.gravity * fallbackTime * fallbackTime;
            }

            float sqrtDisc = Mathf.Sqrt(discriminant);
            float t = (-b - sqrtDisc) / (2f * a);
            if (t < 0f)
            {
                t = (-b + sqrtDisc) / (2f * a);
            }

            t = Mathf.Max(t, 0f);
            return position + velocity * t + 0.5f * Physics.gravity * t * t;
        }

        /// <summary>
        /// Estimates the max height the ball will reach along its current trajectory.
        /// </summary>
        public float EstimateApexHeight(Vector3 position, Vector3 velocity)
        {
            float gravity = Physics.gravity.y;
            if (gravity >= 0f || velocity.y <= 0f)
            {
                return position.y;
            }

            float timeToApex = velocity.y / -gravity;
            return position.y + velocity.y * timeToApex + 0.5f * gravity * timeToApex * timeToApex;
        }

        /// <summary>
        /// Determines if a point is on the agent's side of the net (based on spawn location).
        /// </summary>
        public bool IsOnOurSide(Vector3 point)
        {
            if (netCenter == null)
            {
                return true;
            }

            bool ourSideIsNegative = agentSpawn.x <= netCenter.position.x;
            return ourSideIsNegative ? point.x <= netCenter.position.x : point.x >= netCenter.position.x;
        }

        private void EvaluatePassQuality(Vector3 ballPos, Vector3 ballVelocity)
        {
            Vector3 landingPoint = PredictLandingPoint(ballPos, ballVelocity);
            float apexHeightEstimate = EstimateApexHeight(ballPos, ballVelocity);

            if (!IsOnOurSide(landingPoint))
            {
                passAgent.AddReward(unplayablePenalty);
                return;
            }

            Transform targetRef = passTargetCenter != null ? passTargetCenter : targetZone;
            Vector2 targetSize = passTargetSize.sqrMagnitude > 0f ? passTargetSize : Vector2.one * (targetRadius * 2f);
            bool inPassTarget = targetRef != null && IsInsideBoxXZ(landingPoint, targetRef, targetSize);
            if (inPassTarget)
            {
                Vector2 landingXZ = new Vector2(landingPoint.x, landingPoint.z);
                Vector2 targetXZ = new Vector2(PassTargetPosition.x, PassTargetPosition.z);
                Vector2 extents = targetSize * 0.5f;
                float maxRange = Mathf.Max(extents.x, extents.y);
                float centerDist = Vector2.Distance(landingXZ, targetXZ);
                float centerScore = Mathf.Clamp01(1f - (centerDist / Mathf.Max(maxRange, 0.001f)));

                float apexRequirement = netHeight + apexMargin;
                float apexScore = Mathf.Clamp01((apexHeightEstimate - apexRequirement) / Mathf.Max(apexMargin, 0.001f));

                float blend = Mathf.Clamp01((centerScore + apexScore) * 0.5f);
                float bonus = Mathf.Lerp(passBonusMin, passBonusMax, blend);
                passAgent.AddReward(bonus);
                return;
            }

            if (netCenter != null &&
                Mathf.Abs(landingPoint.x - netCenter.position.x) <= nearNetBuffer &&
                landingPoint.y <= aliveHeight)
            {
                passAgent.AddReward(nearNetPenalty);
                return;
            }

            if (teammateReserveCenter != null &&
                !IsInsideBoxXZ(landingPoint, teammateReserveCenter, teammateReserveSize))
            {
                passAgent.AddReward(unplayablePenalty);
            }
        }

        // Observation & reward summary for quick reference:
        // Observations (from agent):
        //  - Agent pos/vel (XZ)
        //  - Ball relative position (XYZ) & velocity (XYZ)
        //  - Pass target relative position (XZ)
        //  - Touch index fraction, signed distance to net, teammate reserve occupancy flag
        //
        // Rewards:
        //  - Alive reward while ball airborne
        //  - Spacing penalty when standing in teammate reserve
        //  - Contact bonus on hits + pass quality bonus/penalty
        //  - Episode end rewards/penalties (ground, out-of-bounds, success)
        //
        // Inspector wiring:
        //  - passAgent, ballRigidbody, launcher, targetZone
        //  - netCenter, passTargetCenter, teammateReserveCenter
        //  - Configure passTargetSize / teammateReserveSize, reward tuning floats
    }
}

