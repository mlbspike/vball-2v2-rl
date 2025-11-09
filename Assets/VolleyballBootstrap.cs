using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class VolleyballBootstrap : MonoBehaviour
{
    [Header("Court Settings")]
    public float courtLength = 16f;
    public float courtWidth = 8f;
    public float netHeight = 2.43f;
    public float netWidth = 0.1f;
    public float runoff = 3f;
    public float groundY = 0f;

    [Header("Agent Settings")]
    public float agentHeight = 1.9f;
    public float agentRadius = 0.5f;
    public float agentMass = 1f;

    [Header("Ball Settings")]
    public float ballRadius = 0.2f;
    public float ballMass = 0.27f;

    [Header("Serve Settings")]
    public float serveHeight = 2f;
    public float serveForwardImpulse = 4f;
    public float serveDownwardImpulse = 2.3f; // should remain positive

    void Start()
    {
        // Auto-setup when entering play mode
        if (Application.isPlaying)
        {
            SetupScene();
        }
    }

    [ContextMenu("Setup Scene")]
    public void SetupScene()
    {
        // Clean up existing objects if re-running
        CleanupExisting();

        // Create court plane
        GameObject court = CreateCourt();
        
        // Create net
        GameObject net = CreateNet();
        
        // Create ball
        GameObject ball = CreateBall();
        
        // Create agents
        VolleyballAgent[] agents = CreateAgents();
        
        // Create and configure game manager
        VolleyballGameManager gameManager = CreateGameManager(ball, net, agents);
        
        Debug.Log("Volleyball scene setup complete!");
    }

    void CleanupExisting()
    {
        // Remove existing game objects if they exist
        GameObject existingCourt = GameObject.Find("Court");
        if (existingCourt != null) DestroyImmediate(existingCourt);
        
        GameObject existingNet = GameObject.Find("Net");
        if (existingNet != null) DestroyImmediate(existingNet);
        
        GameObject existingBall = GameObject.Find("Ball");
        if (existingBall != null) DestroyImmediate(existingBall);
        
        GameObject existingManager = GameObject.Find("GameManager");
        if (existingManager != null) DestroyImmediate(existingManager);
        
        // Remove existing agents
        for (int i = 0; i < 4; i++)
        {
            GameObject agent = GameObject.Find($"Agent_Team{i / 2}_Player{(i % 2) + 1}");
            if (agent != null) DestroyImmediate(agent);
        }
    }

    GameObject CreateCourt()
    {
        GameObject courtRoot = new GameObject("Court");
        courtRoot.transform.position = new Vector3(0f, groundY, 0f);

        float runoffLength = courtLength + runoff * 2f;
        float runoffWidth = courtWidth + runoff * 2f;

        GameObject runoffPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        runoffPlane.name = "Court_Runoff";
        runoffPlane.transform.SetParent(courtRoot.transform, false);
        runoffPlane.transform.localPosition = Vector3.zero;
        runoffPlane.transform.localScale = new Vector3(runoffLength / 10f, 1f, runoffWidth / 10f);

        Renderer runoffRenderer = runoffPlane.GetComponent<Renderer>();
        Material runoffMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        runoffMat.color = new Color(0.4f, 0.45f, 0.5f);
        runoffRenderer.material = runoffMat;

        GameObject playablePlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        playablePlane.name = "Court_Playable";
        playablePlane.transform.SetParent(courtRoot.transform, false);
        playablePlane.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        playablePlane.transform.localScale = new Vector3(courtLength / 10f, 1f, courtWidth / 10f);

        Renderer playableRenderer = playablePlane.GetComponent<Renderer>();
        Material playableMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        playableMat.color = new Color(0.85f, 0.7f, 0.45f);
        playableRenderer.material = playableMat;

        Collider playableCollider = playablePlane.GetComponent<Collider>();
        if (playableCollider) playableCollider.enabled = false;

        return courtRoot;
    }

    GameObject CreateNet()
    {
        GameObject net = new GameObject("Net");
        net.transform.position = new Vector3(0f, groundY + netHeight / 2f, 0f);
        
        // Create net visual (plane)
        GameObject netVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        netVisual.name = "NetVisual";
        netVisual.transform.parent = net.transform;
        netVisual.transform.localPosition = Vector3.zero;
        netVisual.transform.localScale = new Vector3(netWidth, netHeight, courtWidth);
        
        // Set net material
        Renderer renderer = netVisual.GetComponent<Renderer>();
        Material netMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        netMat.color = Color.white;
        renderer.material = netMat;
        
        // Add collider for net
        BoxCollider netCollider = netVisual.GetComponent<BoxCollider>();
        if (netCollider == null)
        {
            netCollider = netVisual.AddComponent<BoxCollider>();
        }
        netCollider.isTrigger = false;
        
        // Create center point for game manager reference
        GameObject netCenter = new GameObject("NetCenter");
        netCenter.transform.parent = net.transform;
        netCenter.transform.localPosition = Vector3.zero;
        
        return net;
    }

    GameObject CreateBall()
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.transform.position = new Vector3(0f, groundY + 2f, 0f);
        ball.transform.localScale = Vector3.one * ballRadius * 2f;
        
        // Set ball material
        Renderer renderer = ball.GetComponent<Renderer>();
        Material ballMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ballMat.color = new Color(1f, 0.5f, 0f); // Orange
        renderer.material = ballMat;
        
        // Add Rigidbody
        Rigidbody rb = ball.AddComponent<Rigidbody>();
        rb.mass = ballMass;
        rb.drag = 0.5f;
        rb.angularDrag = 0.5f;
        rb.useGravity = true;
        
        // Add physics material for bouncy ball
        PhysicMaterial bouncyMat = new PhysicMaterial("BallPhysics");
        bouncyMat.bounciness = 0.8f;
        bouncyMat.dynamicFriction = 0.3f;
        bouncyMat.staticFriction = 0.3f;
        Collider ballCollider = ball.GetComponent<Collider>();
        ballCollider.material = bouncyMat;
        
        return ball;
    }

    VolleyballAgent[] CreateAgents()
    {
        VolleyballAgent[] agents = new VolleyballAgent[4];
        
        float spawnY = groundY + agentHeight * 0.5f;
        float team0X = -0.35f * courtLength;
        float team1X = 0.35f * courtLength;
        float zOffset = 0.25f * courtWidth;
        
        // Team 0 positions (negative X side)
        Vector3[] team0Positions =
        {
            new Vector3(team0X, spawnY, -zOffset), // Player A
            new Vector3(team0X, spawnY,  zOffset)  // Player B
        };
        
        // Team 1 positions (positive X side)
        Vector3[] team1Positions =
        {
            new Vector3(team1X, spawnY, -zOffset), // Player A
            new Vector3(team1X, spawnY,  zOffset)  // Player B
        };
        
        // Create Team 0 agents
        for (int i = 0; i < 2; i++)
        {
            int teamId = 0;
            string agentName = $"Agent_Team{teamId}_Player{(char)('A' + i)}";
            agents[i] = CreateAgent(agentName, team0Positions[i], teamId);
        }
        
        // Create Team 1 agents
        for (int i = 0; i < 2; i++)
        {
            int teamId = 1;
            string agentName = $"Agent_Team{teamId}_Player{(char)('A' + i)}";
            agents[i + 2] = CreateAgent(agentName, team1Positions[i], teamId);
        }
        
        return agents;
    }

    VolleyballAgent CreateAgent(string name, Vector3 position, int teamId)
    {
        GameObject agentObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        agentObj.name = name;
        agentObj.transform.position = position;
        agentObj.transform.localScale = new Vector3(agentRadius * 2f, agentHeight / 2f, agentRadius * 2f);
        
        // Set agent material based on team
        Renderer renderer = agentObj.GetComponent<Renderer>();
        Material agentMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        agentMat.color = teamId == 0 ? Color.blue : Color.red;
        renderer.material = agentMat;
        
        // Add Rigidbody
        Rigidbody rb = agentObj.AddComponent<Rigidbody>();
        rb.mass = agentMass;
        rb.drag = 5f;
        rb.angularDrag = 5f;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        
        // Add BehaviorParameters
        BehaviorParameters behaviorParams = agentObj.AddComponent<BehaviorParameters>();
        behaviorParams.BehaviorName = "VBall2v2";
        behaviorParams.TeamId = teamId;
        behaviorParams.BrainParameters.VectorObservationSize = 21; // From CollectObservations
        // Note: ActionSpec is automatically determined from the Agent's OnActionReceived method in ML-Agents 4.x
        
        // Add VolleyballAgent
        VolleyballAgent agent = agentObj.AddComponent<VolleyballAgent>();
        agent.teamId = teamId;
        agent.rb = rb;
        
        return agent;
    }

    VolleyballGameManager CreateGameManager(GameObject ball, GameObject net, VolleyballAgent[] agents)
    {
        GameObject managerObj = new GameObject("GameManager");
        VolleyballGameManager gameManager = managerObj.AddComponent<VolleyballGameManager>();
        
        // Assign ball references
        gameManager.ball = ball.transform;
        gameManager.ballRb = ball.GetComponent<Rigidbody>();
        
        // Assign net center
        Transform netCenter = net.transform.Find("NetCenter");
        if (netCenter == null)
        {
            // Fallback: use net transform itself
            netCenter = net.transform;
        }
        gameManager.netCenter = netCenter;
        
        // Assign agents
        gameManager.team0PlayerA = agents[0];
        gameManager.team0PlayerB = agents[1];
        gameManager.team1PlayerA = agents[2];
        gameManager.team1PlayerB = agents[3];
        
        gameManager.courtLength = courtLength;
        gameManager.courtWidth = courtWidth;
        gameManager.runoff = runoff;
        gameManager.groundY = groundY;
        gameManager.playerHeight = agentHeight;
        gameManager.serveHeight = serveHeight;
        gameManager.serveForwardImpulse = serveForwardImpulse;
        gameManager.serveDownwardImpulse = serveDownwardImpulse;
        
        // Assign game manager to all agents
        foreach (var agent in agents)
        {
            agent.gameManager = gameManager;
        }
        
        return gameManager;
    }

    void DestroyImmediate(GameObject obj)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEngine.Object.DestroyImmediate(obj);
        }
        else
#endif
        {
            Destroy(obj);
        }
    }

#if UNITY_EDITOR
    [MenuItem("Volleyball/Setup Scene")]
    static void SetupSceneFromMenu()
    {
        // Find or create bootstrap
        VolleyballBootstrap bootstrap = FindObjectOfType<VolleyballBootstrap>();
        if (bootstrap == null)
        {
            GameObject bootstrapObj = new GameObject("VolleyballBootstrap");
            bootstrap = bootstrapObj.AddComponent<VolleyballBootstrap>();
        }
        
        bootstrap.SetupScene();
    }
#endif
}

