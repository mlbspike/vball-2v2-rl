using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using UnityEngine;

public class VolleyballGameManager : MonoBehaviour
{
    [Header("Refs")]
    public Transform ball;
    public Rigidbody ballRb;
    public Transform netCenter;

    [Header("Court")]
    public float courtLength = 16f;
    public float courtWidth = 8f;
    public float runoff = 3f;
    public float groundY = 0f;

    [Header("Players")]
    public float playerHeight = 1.9f;

    [Header("Agents")]
    public VolleyballAgent team0PlayerA;
    public VolleyballAgent team0PlayerB;
    public VolleyballAgent team1PlayerA;
    public VolleyballAgent team1PlayerB;

    [Header("Serve")]
    public float serveHeight = 2f;
    public float serveForwardImpulse = 4f;
    public float serveDownwardImpulse = 2.3f;

    [Header("Gameplay")]
    public float rallyResetDelay = 1.5f;
    public int maxRalliesPerEpisode = 50;

    // public for observations
    [HideInInspector] public int touchesTeam0;
    [HideInInspector] public int touchesTeam1;
    [HideInInspector] public int lastTouchTeamId = -1;

    SimpleMultiAgentGroup _team0Group;
    SimpleMultiAgentGroup _team1Group;

    int _ralliesThisEpisode;
    bool _rallyLive;
    float _ballRadius = 0.2f;

  //  Leave Awake empty (or remove it)
    void Awake()
    {
        // Intentionally empty: Bootstrap hasn't wired references yet.
    }

    //  Do initialization here, after Bootstrap has assigned all fields
    void Start()
    {
        if (!ballRb && ball) ballRb = ball.GetComponent<Rigidbody>();

        _team0Group = new SimpleMultiAgentGroup();
        _team1Group = new SimpleMultiAgentGroup();

        _team0Group.RegisterAgent(team0PlayerA);
        _team0Group.RegisterAgent(team0PlayerB);
        _team1Group.RegisterAgent(team1PlayerA);
        _team1Group.RegisterAgent(team1PlayerB);

        team0PlayerA.gameManager = this;
        team0PlayerB.gameManager = this;
        team1PlayerA.gameManager = this;
        team1PlayerB.gameManager = this;

        ApplyUniformPlayerHeight();
        if (ball) _ballRadius = ball.lossyScale.y * 0.5f;
        ResetEpisode();
    }

    void ApplyUniformPlayerHeight()
    {
        SetAgentHeight(team0PlayerA);
        SetAgentHeight(team0PlayerB);
        SetAgentHeight(team1PlayerA);
        SetAgentHeight(team1PlayerB);
    }

    void SetAgentHeight(VolleyballAgent agent)
    {
        if (!agent || playerHeight <= 0f) return;

        var capsule = agent.GetComponent<CapsuleCollider>();
        float currentHeight = capsule ? capsule.bounds.size.y : agent.transform.localScale.y * 2f;
        if (currentHeight <= 0f || Mathf.Approximately(currentHeight, playerHeight)) return;

        float scaleFactor = playerHeight / currentHeight;
        agent.transform.localScale = agent.transform.localScale * scaleFactor;
    }

    public VolleyballAgent GetTeammate(VolleyballAgent agent)
    {
        if (agent == team0PlayerA) return team0PlayerB;
        if (agent == team0PlayerB) return team0PlayerA;
        if (agent == team1PlayerA) return team1PlayerB;
        return team1PlayerA;
    }

    public VolleyballAgent[] GetOpponents(VolleyballAgent agent)
    {
        if (agent.teamId == 0)
            return new[] { team1PlayerA, team1PlayerB };
        return new[] { team0PlayerA, team0PlayerB };
    }

    void ResetEpisode()
    {
        _ralliesThisEpisode = 0;
        touchesTeam0 = 0;
        touchesTeam1 = 0;
        lastTouchTeamId = -1;

        ResetAgentsPositions();
        ResetRally(Random.value < 0.5f ? 0 : 1);
    }

    void ResetAgentsPositions()
    {
        float spawnY = groundY + playerHeight * 0.5f;
        float team0X = -0.35f * courtLength;
        float team1X = 0.35f * courtLength;
        float zOffset = 0.25f * courtWidth;

        Vector3[] positions =
        {
            new Vector3(team0X, spawnY, -zOffset),
            new Vector3(team0X, spawnY,  zOffset),
            new Vector3(team1X, spawnY, -zOffset),
            new Vector3(team1X, spawnY,  zOffset)
        };

        VolleyballAgent[] agents = { team0PlayerA, team0PlayerB, team1PlayerA, team1PlayerB };
        for (int i = 0; i < agents.Length; i++)
        {
            var agent = agents[i];
            if (!agent) continue;

            agent.transform.position = positions[i];
            if (agent.rb)
            {
                agent.rb.velocity = Vector3.zero;
                agent.rb.angularVelocity = Vector3.zero;
                agent.rb.position = positions[i];
            }
        }
    }

    void ResetRally()
    {
        ResetRally(Random.value < 0.5f ? 0 : 1);
    }

    void ResetRally(int serveToTeamId)
    {
        _rallyLive = false;
        touchesTeam0 = 0;
        touchesTeam1 = 0;
        lastTouchTeamId = -1;

        if (!ballRb) return;

        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        Vector3 team0ServePos = new Vector3(-courtLength * 0.5f, groundY, -courtWidth * 0.35f);
        Vector3 team1ServePos = new Vector3(courtLength * 0.5f, groundY, courtWidth * 0.35f);
        Vector3 servePos = serveToTeamId == 0 ? team0ServePos : team1ServePos;
        Vector3 startPos = servePos + Vector3.up * serveHeight;

        ballRb.position = startPos;
        if (ball) ball.position = startPos;

        Vector3 targetPos = serveToTeamId == 0
            ? new Vector3(courtLength * 0.25f, groundY, 0f)
            : new Vector3(-courtLength * 0.25f, groundY, 0f);

        Vector3 serveDir = (targetPos - startPos).normalized;
        Vector3 impulse = serveDir * serveForwardImpulse + Vector3.up * serveDownwardImpulse;
        ballRb.AddForce(impulse, ForceMode.Impulse);

        _rallyLive = true;
    }

    public void HitBall(VolleyballAgent agent, Vector3 impulse)
    {
        if (!_rallyLive) return;

        int team = agent.teamId;
        if (team == 0)
        {
            touchesTeam0++;
            touchesTeam1 = 0;
        }
        else
        {
            touchesTeam1++;
            touchesTeam0 = 0;
        }

        lastTouchTeamId = team;
        ballRb.velocity = Vector3.zero;
        ballRb.AddForce(impulse, ForceMode.VelocityChange);

        // Simple illegal 4th-touch check
        if ((touchesTeam0 > 3 && team == 0) ||
            (touchesTeam1 > 3 && team == 1))
        {
            int winningTeam = team == 0 ? 1 : 0;
            ResolveRally(winningTeam, team);
        }
    }

    void FixedUpdate()
    {
        if (!_rallyLive || !ballRb) return;

        EvaluateBallState(ballRb.position);
    }

    public void EvaluateBallState(Vector3 pos)
    {
        if (!_rallyLive || !ballRb) return;

        bool inPlayableCourt =
            Mathf.Abs(pos.x) <= courtLength * 0.5f &&
            Mathf.Abs(pos.z) <= courtWidth * 0.5f;

        bool hardOutOfBounds =
            Mathf.Abs(pos.x) > (courtLength * 0.5f + runoff) ||
            Mathf.Abs(pos.z) > (courtWidth * 0.5f + runoff) ||
            pos.y < -1f;

        float groundContactThreshold = groundY + _ballRadius + 0.02f;

        if (hardOutOfBounds)
        {
            ResolveOutOfBounds(pos);
        }
        else if (pos.y <= groundContactThreshold && ballRb.velocity.y <= 0.1f && inPlayableCourt)
        {
            ResolveInBoundsGroundHit(pos);
        }
        else if (pos.y <= groundContactThreshold && !inPlayableCourt)
        {
            ResolveOutOfBounds(pos);
        }
    }

    void ResolveInBoundsGroundHit(Vector3 pos)
    {
        int winningTeam = pos.x < 0f ? 1 : 0;
        int losingTeam = winningTeam == 0 ? 1 : 0;
        ResolveRally(winningTeam, losingTeam);
    }

    void ResolveOutOfBounds(Vector3 pos)
    {
        if (lastTouchTeamId >= 0)
        {
            int winningTeam = lastTouchTeamId == 0 ? 1 : 0;
            ResolveRally(winningTeam, lastTouchTeamId);
        }
        else
        {
            ResolveRally(null, null);
        }
    }

    void ResolveRally(int? winningTeamId, int? losingTeamId)
    {
        if (!_rallyLive) return;

        _rallyLive = false;
        _ralliesThisEpisode++;

        if (winningTeamId.HasValue && losingTeamId.HasValue)
        {
            if (winningTeamId.Value == 0)
            {
                _team0Group.AddGroupReward(1f);
                _team1Group.AddGroupReward(-1f);
            }
            else
            {
                _team1Group.AddGroupReward(1f);
                _team0Group.AddGroupReward(-1f);
            }
        }
        else
        {
            const float neutralPenalty = -0.1f;
            _team0Group.AddGroupReward(neutralPenalty);
            _team1Group.AddGroupReward(neutralPenalty);
        }

        if (ballRb)
        {
            ballRb.velocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
        }

        _team0Group.EndGroupEpisode();
        _team1Group.EndGroupEpisode();

        ResetEpisode();
    }
}
