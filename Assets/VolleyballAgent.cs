using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VolleyballAgent : Agent
{
    [Header("Setup")]
    public VolleyballGameManager gameManager;
    public Rigidbody rb;
    public int teamId; // 0 or 1, set in inspector to match BehaviorParameters TeamId

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 6f;
    public float maxHorizontalSpeed = 8f;

    [Header("Hit")]
    public float hitRadius = 1.2f;
    public float hitUpwardFactor = 4f;
    public float maxHitForce = 9f;

    // internal
    Vector3 _spawnPos;

    public override void Initialize()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        _spawnPos = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        // Reset handled by GameManager; just ensure rigidbody is calm.
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = _spawnPos;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var ball = gameManager.ball;
        var myPos = transform.position;
        var myVel = rb.velocity;

        // Court / side info
        sensor.AddObservation(teamId); // 0 or 1

        // Self
        sensor.AddObservation(myPos.x);
        sensor.AddObservation(myPos.z);
        sensor.AddObservation(myVel.x);
        sensor.AddObservation(myVel.z);
        sensor.AddObservation(myVel.y);

        // Ball
        sensor.AddObservation(ball.position.x);
        sensor.AddObservation(ball.position.y);
        sensor.AddObservation(ball.position.z);
        sensor.AddObservation(gameManager.ballRb.velocity.x);
        sensor.AddObservation(gameManager.ballRb.velocity.y);
        sensor.AddObservation(gameManager.ballRb.velocity.z);

        // Teammate & opponents (positions only for now)
        var mate = gameManager.GetTeammate(this);
        var opps = gameManager.GetOpponents(this);

        sensor.AddObservation(mate.transform.position.x);
        sensor.AddObservation(mate.transform.position.z);

        sensor.AddObservation(opps[0].transform.position.x);
        sensor.AddObservation(opps[0].transform.position.z);
        sensor.AddObservation(opps[1].transform.position.x);
        sensor.AddObservation(opps[1].transform.position.z);

        // Touch counts + who touched last (for basics)
        sensor.AddObservation(gameManager.touchesTeam0);
        sensor.AddObservation(gameManager.touchesTeam1);
        sensor.AddObservation(gameManager.lastTouchTeamId); // -1 none, 0,1
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var c = actions.ContinuousActions;
        float moveX = Mathf.Clamp(c[0], -1f, 1f);
        float moveZ = Mathf.Clamp(c[1], -1f, 1f);
        float jumpCmd = Mathf.Clamp01(c[2]);
        float hitDirX = Mathf.Clamp(c[3], -1f, 1f);
        float hitDirZ = Mathf.Clamp(c[4], -1f, 1f);
        float hitPower = Mathf.Clamp01(c[5]);

        // Movement on XZ
        var vel = rb.velocity;
        var desired = new Vector3(moveX, 0f, moveZ) * moveSpeed;
        var horiz = new Vector3(vel.x, 0f, vel.z);
        var accel = desired - horiz;
        rb.AddForce(accel, ForceMode.Acceleration);

        // Limit horizontal speed
        horiz = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (horiz.magnitude > maxHorizontalSpeed)
        {
            var clamped = horiz.normalized * maxHorizontalSpeed;
            rb.velocity = new Vector3(clamped.x, rb.velocity.y, clamped.z);
        }

        // Jump (single simple grounded check)
        if (jumpCmd > 0.5f && IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }

        // Try hit if close to ball
        TryHitBall(hitDirX, hitDirZ, hitPower);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Optional: keyboard debug control
        var c = actionsOut.ContinuousActions;
        c[0] = Input.GetAxis("Horizontal");
        c[1] = Input.GetAxis("Vertical");
        c[2] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
        c[3] = 0f;
        c[4] = 1f;
        c[5] = 0.8f;
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }

    void TryHitBall(float dirX, float dirZ, float power01)
    {
        var ballPos = gameManager.ball.position;
        var toBall = ballPos - transform.position;
        toBall.y = 0f;

        if (toBall.magnitude <= hitRadius &&
            gameManager.ball.position.y <= transform.position.y + 2.2f)
        {
            // Construct hit direction
            var dir = new Vector3(dirX, 0f, dirZ);
            if (dir.sqrMagnitude < 0.1f)
            {
                // default: aim over net center
                var netCenter = gameManager.netCenter.position;
                dir = (new Vector3(netCenter.x, 0f, netCenter.z) - new Vector3(transform.position.x, 0f, transform.position.z));
            }
            dir = dir.normalized;

            float power = Mathf.Lerp(3f, maxHitForce, power01);
            var impulse = dir * power + Vector3.up * hitUpwardFactor;

            gameManager.HitBall(this, impulse);
        }
    }
}
