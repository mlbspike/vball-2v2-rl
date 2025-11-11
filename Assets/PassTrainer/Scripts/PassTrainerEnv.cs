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

        [Header("Rewards")]
        [SerializeField] private float contactBonus = 0.1f;
        [SerializeField] private float successReward = 1.0f;
        [SerializeField] private float shapingScale = 0.5f;
        [SerializeField] private float maxDistForShaping = 6f;
        [SerializeField] private float noTouchPenalty = -0.2f;

        private bool _episodeActive;
        private bool _touchedBall;
        private float _bestTargetError;

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

        private void Awake()
        {
            TryAutoCreate();
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
                Vector3 targetPos = targetZone != null ? targetZone.position : agentPos;
                Vector3 toTarget = targetPos - ballPos;
                toTarget.y = Mathf.Max(toTarget.y, 0f) + launchArcY;
                Vector3 passDir = toTarget.normalized;

                ballRigidbody.velocity = passDir * passSpeed;

                passAgent.AddReward(contactBonus);
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

        private void OnBallDead(bool inTarget, bool outOfBounds, Vector3 landingPos)
        {
            if (!_episodeActive)
            {
                return;
            }

            _episodeActive = false;

            float reward = 0f;
            if (inTarget)
            {
                reward += successReward;
            }
            else
            {
                if (_touchedBall && _bestTargetError < float.MaxValue)
                {
                    float norm = Mathf.Clamp01(1f - (_bestTargetError / Mathf.Max(maxDistForShaping, 0.0001f)));
                    reward += shapingScale * norm;
                }

                if (!_touchedBall)
                {
                    reward += noTouchPenalty;
                }
            }

            passAgent.AddReward(reward);
            passAgent.EndEpisode();
        }
    }
}

