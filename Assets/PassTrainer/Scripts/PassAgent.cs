using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace PassTrainer
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BehaviorParameters))]
    public class PassAgent : Agent
    {
        [SerializeField] private PassTrainerEnv env;
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 6f;

        private Rigidbody _rigidbody;
        private BehaviorParameters _behaviorParameters;
        private bool _isGrounded;

        private static readonly int[] ActionBranchSizes = { 3, 3, 2, 2 };

        protected override void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _behaviorParameters = GetComponent<BehaviorParameters>();
            EnsureBehaviorParameters();
        }

        private void FixedUpdate()
        {
            UpdateGroundedState();
        }

        public override void Initialize()
        {
            base.Initialize();
            EnsureBehaviorParameters();
        }

        public override void OnEpisodeBegin()
        {
            if (env != null)
            {
                env.BeginEpisode();
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            Vector3 agentPosition = transform.position;
            Vector3 agentVelocity = _rigidbody.velocity;

            sensor.AddObservation(agentPosition.x);
            sensor.AddObservation(agentPosition.z);

            sensor.AddObservation(agentVelocity.x);
            sensor.AddObservation(agentVelocity.z);

            if (env != null && env.BallRigidbody != null)
            {
                Vector3 relativeBallPos = env.BallRigidbody.position - agentPosition;
                Vector3 ballVelocity = env.BallRigidbody.velocity;

                sensor.AddObservation(relativeBallPos.x);
                sensor.AddObservation(relativeBallPos.y);
                sensor.AddObservation(relativeBallPos.z);

                sensor.AddObservation(ballVelocity.x);
                sensor.AddObservation(ballVelocity.y);
                sensor.AddObservation(ballVelocity.z);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(Vector3.zero);
            }

            if (env != null && env.TargetZone != null)
            {
                Vector3 relativeTargetPos = env.TargetZone.position - agentPosition;
                sensor.AddObservation(relativeTargetPos.x);
                sensor.AddObservation(relativeTargetPos.z);
            }
            else
            {
                sensor.AddObservation(Vector2.zero);
            }
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            var discreteActions = actionBuffers.DiscreteActions;

            int moveX = discreteActions[0] - 1; // -1, 0, 1
            int moveZ = discreteActions[1] - 1; // -1, 0, 1
            bool jump = discreteActions[2] == 1;
            bool hit = discreteActions[3] == 1;

            Vector3 velocity = _rigidbody.velocity;
            velocity.x = moveX * moveSpeed;
            velocity.z = moveZ * moveSpeed;
            _rigidbody.velocity = velocity;

            if (jump && _isGrounded)
            {
                _rigidbody.velocity = new Vector3(_rigidbody.velocity.x, 0f, _rigidbody.velocity.z);
                _rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            }

            if (hit && env != null)
            {
                env.TryHitBall(this);
            }

            AddReward(-0.001f);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActions = actionsOut.DiscreteActions;
            discreteActions[0] = 1;
            discreteActions[1] = 1;
            discreteActions[2] = 0;
            discreteActions[3] = 0;
        }

        private void EnsureBehaviorParameters()
        {
            if (_behaviorParameters == null)
            {
                return;
            }

            _behaviorParameters.BehaviorName = "VBallPassTrainer";
            _behaviorParameters.BehaviorType = BehaviorType.Default;
            _behaviorParameters.TeamId = 0;

        }

        private void UpdateGroundedState()
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            const float rayDistance = 0.2f;
            _isGrounded = Physics.Raycast(origin, Vector3.down, rayDistance);
        }

        public void SetEnvironment(PassTrainerEnv trainerEnv)
        {
            env = trainerEnv;
        }

        public void ResetRigidbodyState()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }
}

