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

        protected override void Awake()
        {
            base.Awake();
            _rb = GetComponent<Rigidbody>();
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
            Vector3 targetPos = env != null ? env.PassTargetPosition : Vector3.zero;
            Vector3 netPos = env != null ? env.NetPosition : Vector3.zero;

            float normX = Mathf.Max(env != null ? env.CourtHalfLength : 8f, 0.001f);
            float normZ = Mathf.Max(env != null ? env.CourtHalfWidth : 4f, 0.001f);
            float normV = 10f;

            // Agent pos (x,z) on court
            sensor.AddObservation(pos.x / normX);
            sensor.AddObservation(pos.z / normZ);

            // Agent vel (x,z) normalized
            sensor.AddObservation(vel.x / normV);
            sensor.AddObservation(vel.z / normV);

            // Ball relative position (x,y,z) in agent frame
            Vector3 ballRel = ballPos - pos;
            sensor.AddObservation(ballRel.x / normX);
            sensor.AddObservation(ballRel.y / normX);
            sensor.AddObservation(ballRel.z / normZ);

            // Ball velocity (x,y,z)
            sensor.AddObservation(ballVel.x / normV);
            sensor.AddObservation(ballVel.y / normV);
            sensor.AddObservation(ballVel.z / normV);

            // Pass target relative position (x,z)
            Vector3 targetRel = targetPos - pos;
            sensor.AddObservation(targetRel.x / normX);
            sensor.AddObservation(targetRel.z / normZ);

            // Touch index (0,1,2) normalized
            float touchFraction = env != null ? Mathf.Clamp01(env.TouchIndex / 2f) : 0f;
            sensor.AddObservation(touchFraction);

            // Signed distance to net along court length
            float netDepth = env != null ? (pos.x - netPos.x) / normX : 0f;
            sensor.AddObservation(Mathf.Clamp(netDepth, -1f, 1f));

            // Teammate reserve occupancy (0 or 1)
            bool inReserve = false;
            if (env != null && env.HasTeammateReserve)
            {
                Vector3 reservePos = env.TeammateReservePosition;
                Vector2 reserveSize = env.TeammateReserveExtents;
                if (reserveSize.sqrMagnitude > 0f)
                {
                    float halfX = reserveSize.x * 0.5f;
                    float halfZ = reserveSize.y * 0.5f;
                    inReserve = Mathf.Abs(pos.x - reservePos.x) <= halfX &&
                                Mathf.Abs(pos.z - reservePos.z) <= halfZ;
                }
            }
            sensor.AddObservation(inReserve ? 1f : 0f);
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

            if (env != null)
            {
                env.ApplyStepRewards(this);
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

        // Observation order (CollectObservations):
        // 1-2: Agent world position (x,z) normalised by court half-length/width
        // 3-4: Agent velocity (x,z)
        // 5-7: Ball relative position (x,y,z)
        // 8-10: Ball velocity (x,y,z)
        // 11-12: Pass target relative position (x,z)
        // 13: Normalised touch index (0-1)
        // 14: Signed distance to net ([-1,1])
        // 15: Teammate reserve occupancy flag
        //
        // Reward hooks (see PassTrainerEnv):
        //  - Step penalty (-0.001f) + alive reward + spacing penalty
        //  - Contact bonus and pass quality bonuses/penalties
        //  - Episode terminal rewards (ground hit, out-of-bounds, success)
        //
        // Inspector setup:
        //  - Assign PassTrainerEnv reference
        //  - Tweak moveSpeed/jumpForce plus ground check distance & mask as needed
    }
}
