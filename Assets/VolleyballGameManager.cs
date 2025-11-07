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
    public float courtHalfLength = 8f;
    public float courtHalfWidth = 4f;

    [Header("Agents")]
    public VolleyballAgent team0PlayerA;
    public VolleyballAgent team0PlayerB;
    public VolleyballAgent team1PlayerA;
    public VolleyballAgent team1PlayerB;

    [Header("Gameplay")]
    public float serveHeight = 2f;
    public float serveForce = 5f;
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

  //  Leave Awake empty (or remove it)
    void Awake()
    {
        // Intentionally empty: Bootstrap hasn't wired references yet.
    }

    //  Do initialization here, after Bootstrap has assigned all fields
    void Start()
    {
        if (!ballRb) ballRb = ball.GetComponent<Rigidbody>();

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
        ResetEpisode();
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
        StartRally(serveToTeamId: Random.value < 0.5f ? 0 : 1);
    }

    void ResetAgentsPositions()
    {
        // very simple spawn layout; tweak later
        team0PlayerA.transform.position = new Vector3(-2f, 1f, -1.5f);
        team0PlayerB.transform.position = new Vector3( 2f, 1f, -1.5f);
        team1PlayerA.transform.position = new Vector3(-2f, 1f,  1.5f);
        team1PlayerB.transform.position = new Vector3( 2f, 1f,  1.5f);

        foreach (var a in new[] { team0PlayerA, team0PlayerB, team1PlayerA, team1PlayerB })
        {
            a.rb.velocity = Vector3.zero;
            a.rb.angularVelocity = Vector3.zero;
        }
    }

    void StartRally(int serveToTeamId)
    {
        _rallyLive = false;
        touchesTeam0 = 0;
        touchesTeam1 = 0;
        lastTouchTeamId = -1;

        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        // Simple center serve from opposite side
        float z = serveToTeamId == 0 ? 1.0f : -1.0f;
        ball.position = new Vector3(0f, serveHeight, z);
        var dir = new Vector3(0f, 0.2f, serveToTeamId == 0 ? -1f : 1f).normalized;
        ballRb.AddForce(dir * serveForce, ForceMode.VelocityChange);

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
            EndRally(losingTeamId: team);
        }
    }

    void FixedUpdate()
    {
        if (!_rallyLive) return;

        // Check ball out-of-bounds / floor contact
        if (ball.position.y < 0.5f)
        {
            int side = ball.position.z < 0 ? 0 : 1; // which court half it landed on
            EndRally(losingTeamId: side);
            return;
        }

        // Soft bounds: if ball goes way out sides/back
        if (Mathf.Abs(ball.position.x) > courtHalfWidth + 2f ||
            Mathf.Abs(ball.position.z) > courtHalfLength + 2f ||
            ball.position.y > 15f)
        {
            // If lastTouch known, that team loses
            int losing = lastTouchTeamId >= 0 ? lastTouchTeamId : 0;
            EndRally(losingTeamId: losing);
        }
    }

    void EndRally(int losingTeamId)
    {
        if (!_rallyLive) return;
        _rallyLive = false;
        _ralliesThisEpisode++;

        int winningTeamId = losingTeamId == 0 ? 1 : 0;

        float winReward = 0.3f;
        float loseReward = -0.3f;

        if (winningTeamId == 0)
        {
            _team0Group.AddGroupReward(winReward);
            _team1Group.AddGroupReward(loseReward);
        }
        else
        {
            _team1Group.AddGroupReward(winReward);
            _team0Group.AddGroupReward(loseReward);
        }

        if (_ralliesThisEpisode >= maxRalliesPerEpisode)
        {
            _team0Group.EndGroupEpisode();
            _team1Group.EndGroupEpisode();
            ResetEpisode();
        }
        else
        {
            // Endless style: reset just rally
            Invoke(nameof(StartNextRally), rallyResetDelay);
        }
    }

    void StartNextRally()
    {
        int serveTo = Random.value < 0.5f ? 0 : 1;
        StartRally(serveTo);
    }
}
