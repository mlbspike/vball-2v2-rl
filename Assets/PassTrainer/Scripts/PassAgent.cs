using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

namespace PassTrainer
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BehaviorParameters))]
    public class PassAgent : Agent
    {
        [Header("Env Ref")]
        [SerializeField] private PassTrainerEnv env;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 6f;

        [Header("Ground Check")]
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private LayerMask groundMask = ~0;

        private Rigidbody _rb;
        private BehaviorParameters _behaviorParameters;

        protected override void Awake()
        {
            base.Awake();
            _rb = GetComponent<Rigidbody>();
            _behaviorParameters = GetComponent<BehaviorParameters>();
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
            Vector3 pos = transform.position;
            Vector3 vel = _rb.velocity;

            Vector3 ballPos = env != null ? env.BallPosition : Vector3.zero;
            Vector3 ballVel = env != null ? env.BallVelocity : Vector3.zero;
            Vector3 targetPos = env != null ? env.TargetPosition : Vector3.zero;

            float normX = 8f;
            float normZ = 4f;
            float normV = 10f;

            // Agent pos (x,z)
            sensor.AddObservation(pos.x / normX);
            sensor.AddObservation(pos.z / normZ);

            // Agent vel (x,z)
            sensor.AddObservation(vel.x / normV);
            sensor.AddObservation(vel.z / normV);

            // Ball relative to agent
            Vector3 ballRel = ballPos - pos;
            sensor.AddObservation(ballRel.x / normX);
            sensor.AddObservation(ballRel.y / normX);
            sensor.AddObservation(ballRel.z / normZ);

            // Ball vel
            sensor.AddObservation(ballVel.x / normV);
            sensor.AddObservation(ballVel.y / normV);
            sensor.AddObservation(ballVel.z / normV);

            // Target relative to agent
            Vector3 targetRel = targetPos - pos;
            sensor.AddObservation(targetRel.x / normX);
            sensor.AddObservation(targetRel.z / normZ);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var d = actions.DiscreteActions;

            int moveX = d.Length > 0 ? d[0] : 1;
            int moveZ = d.Length > 1 ? d[1] : 1;
            int jump  = d.Length > 2 ? d[2] : 0;
            int hit   = d.Length > 3 ? d[3] : 0;

            Vector3 move = Vector3.zero;

            // X: 0=L,1=none,2=R
            if (moveX == 0) move += Vector3.left;
            else if (moveX == 2) move += Vector3.right;

            // Z: 0=B,1=none,2=F
            if (moveZ == 0) move += Vector3.back;
            else if (moveZ == 2) move += Vector3.forward;

            if (move.sqrMagnitude > 1f)
                move.Normalize();

            // Apply movement
            Vector3 v = _rb.velocity;
            v.x = move.x * moveSpeed;
            v.z = move.z * moveSpeed;

            // Jump
            if (jump == 1 && IsGrounded())
            {
                v.y = jumpForce;
            }

            _rb.velocity = v;

            // Hit
            if (hit == 1 && env != null)
            {
                env.TryHitBall(this);
            }

            AddReward(-0.001f);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var d = actionsOut.DiscreteActions;

            d[0] = 1; // X none
            d[1] = 1; // Z none
            d[2] = 0; // no jump
            d[3] = 0; // no hit

            if (env == null)
                return;

            Vector3 ballPos = env.BallPosition;
            Vector3 toBall = ballPos - transform.position;
            float deadZone = 0.2f;

            // X toward ball
            if (toBall.x < -deadZone) d[0] = 0;
            else if (toBall.x > deadZone) d[0] = 2;

            // Z toward ball
            if (toBall.z < -deadZone) d[1] = 0;
            else if (toBall.z > deadZone) d[1] = 2;

            // Jump if ball high
            if (ballPos.y > 1.5f)
                d[2] = 1;

            // Hit if close and under height
            float hr = env.HitRadius;
            float hh = env.HitMaxHeight;

            if (Mathf.Abs(toBall.x) <= hr &&
                Mathf.Abs(toBall.z) <= hr &&
                ballPos.y <= hh)
            {
                d[3] = 1;
            }
        }

        private bool IsGrounded()
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            return Physics.Raycast(origin, Vector3.down,
                groundCheckDistance + 0.1f,
                groundMask, QueryTriggerInteraction.Ignore);
        }
    }
}
