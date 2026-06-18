using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using System.Threading;
using static UnityEngine.EventSystems.EventTrigger;


[RequireComponent(typeof(LineRenderer))]
public class WaveSpawner : MonoBehaviour
{
    // communication with HandleExperiment.cs

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
    private const int VK_F6 = 0x75;
    private const uint KEYEVENTF_KEYDOWN = 0x0;

    public GameObject obstacle;
    public string handleExpeObjectName;

    // initial parameters for the wave and filling curve

    public float amplitude = 100f; // the amplitude of splits
    public float ySpacing = 2f; // the spacing between points 
    public float waveSpeed = 300f; // the speed of the wave moving downwards
    public float waveLength = 500f; // the half-length of the splits
    public float startY = 2000f; // center of the first split
    public float splitspacings = 4000f; // the spacing between splits
    public float ObstacleProbability = 0.2f; // the probability of an obstacle not being visible to the master player in the Obstacle Visibility = 1 or 2 conditions
    public float LoneObstacleProbability = 0.2f; // the probability of an obstacle not being visible to the both player in the Lone Trials (ctrlMode = 1 or 2)

    // the oscilating part coefficients (f function in fillingcure)

    public float[] sineAmplitudes = new float[] { 2f, 5f, 10f, 20f }; // the amplitudes of the sine waves that modify the filling curve between the splits
    public float[] sineWavelengths = new float[] { 3f, 8f, 10f, 50f }; // the wavelengths of the sine waves that modify the filling curve between the splits

    // defining the desired left and right trajectories
    
    private List<Vector3> trajectoryPointsR = new List<Vector3>(); // the points of the right desired trajectory
    private List<Vector3> trajectoryPointsL = new List<Vector3>(); // the points of the left desired trajectory
    private List<int> ObstacleFlags = new List<int>(); // the flags for the obstacles, which are used to determine if the obstacle has reached the cursor level (y = -200f)
    private List<int> ObstacleVisFlags = new List<int>(); // the visibility flags of the obstacles, which are used to determine if the obstacle is visible to the player or not
    private List<bool> ReachFlag = new List<bool>(); // each i-th element of the trajectory can either have 1 (higher than the cursor level) or 0 (lower than the cursor level) flag, which is used to determine if the trajectory has reached the cursor level (y = -200f)
    private List<bool> ObsFlag = new List<bool>(); // each j-th obstacle can either be 1 (above cursor lever) or 0 (below cursor level) flag, which is used to determine if the obstacle has reached the cursor level (y = -200f)
    private int currentSplitIndex = 0;

    public LineRenderer lineRendererR;
    public LineRenderer lineRendererL;

    public static float xdR = 0f;
    public static float xdL = 0f;
    public static int LiveObs, VisObs; // LiveObs: -1 for right, -1 for left, 0 for no obstacle; VisObs: 0 for invisible obstacle, 1 for visibile obstacle for player one, 2 for visible obstacle for player two, 3 for visible obstacle for both players
    private int flag = 100; // flag to determine if the obstacle is on the left or right side of the split, 1 for right, -1 for left, 0 for no obstacle
    private int obsvisflag, obsflag;

    // to be sent to HandleExperiment.cs

    public static List<float> waveX = new List<float>(); // the list of x-coordinates of the center of the splits
    public static List<float> waveY = new List<float>(); // the list of y-coordinates of the center of the splits
    public static List<Vector3> obstaclePositions = new List<Vector3>();
    public static List<float> obstacleHitTimes = new List<float>(); // store the timing when the obstacles will hit the cursor level (y = -200f)
    public static List<bool> isSplit = new List<bool>(); // 1: split has an obstacle, 0: split has no obstacle
    public static List<GameObject> activeObstacles = new List<GameObject>(); // store the obstacles
    public static float StartTime; // the time when EMG starts recording (F5 is virtually pressed in HandleExpe.cs)
    public static bool end;

    private GameObject handleExpeObject;
    private bool boolStart = false; // flag to check if the wave spawning has started

    private int ctrlMode, ObstaclesVisibility; // control mode and Obstacle Visibility, which are set in HandleExpe.cs in the master canvas by the user
    
    // the mesh that is used as the margin
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;

    // create the marginal mesh
    private MeshFilter meshFilter;
    public Material meshMaterial;
    public Color meshColor = new Color(0f, 0f, 0f, 0f);
    private float marginSize =  HandleExpe.margin_size; // The margin size to extend the mesh beyond the trajectory
    private MeshRenderer meshRenderer;
    public string meshSortingLayer = "Default";
    public int meshSortingOrder = -10;   // negative = behind sprites/lines


    const float PI = 3.1415927f;

    void Start()
    {
        obstacle.transform.localScale = new Vector3(4*amplitude, 80f, 1f); // scale x, y, z of the obstacle
        //Thread.Sleep(10); // wait for 10 ms to ensure that the HandleExpe.cs is loaded and the user has set the control mode and obstacle visibility in the master canvas
        // get the control mode and obstacle visibility from HandleExpe.cs
        handleExpeObject = GameObject.Find(handleExpeObjectName);

        // line renderers initialisation

        lineRendererL.positionCount = 0;
        lineRendererL.widthMultiplier = 20f;
        lineRendererL.material = new Material(Shader.Find("Sprites/Default"));
        lineRendererL.startColor = Color.white;
        lineRendererL.endColor = Color.white;
        lineRendererL.useWorldSpace = true;

        lineRendererR.positionCount = 0;
        lineRendererR.widthMultiplier = 20f;
        lineRendererR.material = new Material(Shader.Find("Sprites/Default"));
        lineRendererR.startColor = Color.white;
        lineRendererR.endColor = Color.white;
        lineRendererR.useWorldSpace = true;

        // initialise the positions of the center of the splits

        //waveX = new List<float> { 0f, -50f, 50f, -50f, -50f, 50f, -50f, 0f, 50f, -50f, 0f, 50f };
        waveX = new List<float> { 0f, -20f, 20f, -20f, -20f, 20f, -20f, 0f, 20F };

        waveY = new List<float>();
        for (int i = 0; i < waveX.Count; i++)
        {
            waveY.Add(startY + i * splitspacings);
        }
            StartCoroutine(SpawnAllWaves());

        // create the marginal mesh
        //mesh = new Mesh();
        //meshFilter = GetComponent<MeshFilter>();
        //meshFilter.mesh = mesh;
        //meshRenderer = GetComponent<MeshRenderer>();
        //if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        meshRenderer.material = meshMaterial; // IMPORTANT: assign your material here

        meshRenderer.sortingLayerName = meshSortingLayer;
        meshRenderer.sortingOrder = meshSortingOrder;
    }

    void Update()
    {
        end = false; // the game has just started
        boolStart = handleExpeObject.GetComponent<HandleExpe>().myBooleanStart; // check if the experiment has started through the start button
        ctrlMode = HandleExpe.ctrlMode; // get the control mode from HandleExpe.cs
        //Debug.Log("Control Mode is :" + ctrlMode);
        ObstaclesVisibility = HandleExpe.ObstaclesVisibility; // get the obstacle visibility from HandleExpe.cs
        float relativetime = HandleExpe.relativetime; // get the relative time from HandleExpe.cs

        if (boolStart)
        {
            //if (HandleExpe.FirstobstHit == 1 || HandleExpe.SecondobstHit == 1)
                //return;

                //startflag = true;
                float dy = waveSpeed * Time.deltaTime; // how much the wave moves downwards in this frame

                // Move left and right trajectory points downwards
                for (int i = 0; i < trajectoryPointsL.Count; i++)
                {
                    trajectoryPointsL[i] -= new Vector3(0f, dy, 0f);
                    trajectoryPointsR[i] -= new Vector3(0f, dy, 0f);

                    if (trajectoryPointsL[i].y <= -200f && ReachFlag[i] == true)
                    {
                        ReachFlag[i] = false; // passed cursor level

                        LiveObs = ObstacleFlags[i]; // live obstacle flag, which is used to determine if the obstacle is on the left or right side of the split, 1 for right, -1 for left, 0 for no obstacle, 100 for the filling curve
                        VisObs = ObstacleVisFlags[i]; // visibility of the obstacle to be sent to handleExpe.cs to be saved. 0 for invisible obstacle, 1 for visible obstacle for player one, 2 for visible obstacle for player two, 3 for visible obstacle for both players
                        xdL = trajectoryPointsL[i].x;
                        xdR = trajectoryPointsR[i].x;


                        // Determine if we're in a split region or a filling region
                        bool inSplit = Mathf.Abs(xdR - xdL) > 1e-3f;

                        if (!inSplit)
                        {
                            LiveObs = 100; // not a split (filling)
                            VisObs = 100;
                        }

                        //if (LiveObs == 1)
                        //{
                        //    xdR = xdL;
                        //}
                        //if (LiveObs == -1)
                        //{
                        //    xdL = xdR;
                        //}

                        //Debug.Log($"xdL = {xdL}, xdR = {xdR}, VisObs = {VisObs}, LiveObs = {LiveObs}, Time: {relativetime}" );

                    }

                    // check if the trajectory has ended (y < -300) and reset ctrlMode to 1 in HandleExpe.cs
                    if (trajectoryPointsL.Count > 0 && trajectoryPointsL[trajectoryPointsL.Count - 1].y < -300f)
                    {
                        // Simulate F6 key press
                        keybd_event(VK_F6, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);

                        float endreltime = relativetime; // the relative time (time - StartTime) when the trajectory has ended
                        Debug.Log("Trajectory ended: ctrlMode reset to 1, F6 pressed. End absolute Time is:" + Time.time + "End ralative Time is:" + endreltime);
                        end = true; // the trajectory has ended, set the end flag to true to be sent to HandleExpe.cs
                        Application.Quit(); // quit the application after the trajectory has ended
                    }
                }

                // Move obstacles downwards
                for (int j = 0; j < activeObstacles.Count; j++)
                {
                    GameObject obs = activeObstacles[j];
                    obs.transform.position -= new Vector3(0f, dy, 0f);

                    // Check if the obstacle is now at cursor level
                    if (obs.transform.position.y < -200f && ObsFlag[j] == true)
                    {
                        ObsFlag[j] = false; // obstacle is below the cursor level
                        float hittime = relativetime; // the relative time (time - StartTime) when the obstacle hit the cursor level
                        obstacleHitTimes.Add(hittime); // store current time of hit
                        obstaclePositions.Add(obs.transform.position); // store obstacle position
                        Debug.Log("Obstacle hit at: " + obs.transform.position + " at relative time: " + hittime);
                    }
                }

                // render the line segments for left and right trajectories 

                lineRendererL.positionCount = trajectoryPointsL.Count;
                lineRendererL.SetPositions(trajectoryPointsL.ToArray());

                lineRendererR.positionCount = trajectoryPointsR.Count;
                lineRendererR.SetPositions(trajectoryPointsR.ToArray());

                // display the obstacles hit times 

                //Debug.Log("Obstacle Hit Times: " + string.Join(", ", obstacleHitTimes));

                //UpdateMesh();


        }
    }

    IEnumerator SpawnAllWaves()
    {
        Vector2 lastPoint = Vector2.zero;

        FillCurveBetween(0, 500, 0, waveY[0] - waveLength); // the initial curve before the first split

        for (int i = 0; i < waveX.Count - 1 && i < waveY.Count - 1; i++)
        {
            float xCenter = waveX[i];
            float xNextCenter = waveX[i + 1];
            float yCenter = waveY[i];
            float yNextCenter = waveY[i + 1];

            SpawnWave(xCenter, yCenter); // spawn splits and obstacles

            FillCurveBetween(xCenter, yCenter + waveLength, xNextCenter, yNextCenter - waveLength); // spawn the filling curve between the splits

            yield return null;
        }
        DrawLine();
    }
    
    //  split-spawning function
    Vector2 SpawnWave(float xCenter, float yCenter)
    {
        List<Vector3> left = new List<Vector3>();
        List<Vector3> right = new List<Vector3>();
        List<int> obs = new List<int>(); // obstacle flags, 1 for right, -1 for left, 0 for no obstacle
        List<int> obsvis = new List<int>(); // visibility flags of the obstacles, 0 for invisible, 1 for visible to player 1, 2 for visible to player 2, 3 for visible to both players

        bool obstacleSpawned = false;
        bool leftHasObstacle = false;
        bool rightHasObstacle = false;

        float ySplitStart = yCenter - waveLength;
        float ySplitEnd = yCenter + waveLength;

        // generate the obstacles

        //float yobs = yCenter - 0.5f * waveLength;
        float yobs = yCenter ;

        float cosValobs = Mathf.Cos(PI * (yobs - yCenter) / waveLength);
        float baseXobs = amplitude * (1f + cosValobs);
        float envelopeobs = Mathf.Sin(PI * ((yobs - ySplitStart) / (ySplitEnd - ySplitStart)));
        float oscobs = Mathf.Sin(8f * PI * ((yobs - ySplitStart) / (ySplitEnd - ySplitStart)));
        float extraobs = 40f * envelopeobs * oscobs; // adding oscilation to the split 

        //float x1obs = xCenter + baseXobs + extraobs;
        //float x2obs = xCenter - baseXobs - extraobs;

        float x1obs = xCenter + baseXobs ;
        float x2obs = xCenter - baseXobs ;

        if (!obstacleSpawned)
        {
            obstacleSpawned = true;
            float r = UnityEngine.Random.value;
            if (r < 1f / 2f) // spawn obstacle on the right branch
            {
                obsflag = CreateObstacle(xCenter + marginSize, yobs);
                rightHasObstacle = true;
                flag = 1;
                obs.Add(1); // store the obstacle position
            }
            else //if (r < 2f / 3f)
            {
                obsflag = CreateObstacle(xCenter - marginSize, yobs);
                leftHasObstacle = true;
                flag = -1;
                obs.Add(-1); // store the obstacle position
            }
        }

        for (float y = ySplitStart; y <= ySplitEnd; y += ySpacing)
        {
            obsvis.Add(obsflag); // store the visibility flag of the obstacle throughout the split

            // fil ObstacleFlags
            if (obsflag == 0)
            {
                obs.Add(0);
            }
            else
            {
                if (flag == 1)
                {
                    obs.Add(1); // obstacles on the right side
                }
                else if (flag == -1)
                {
                    obs.Add(-1); // obstacles on the left side
                }
            }


            // generate the split points

            float envelope = Mathf.Sin(PI * ((y - ySplitStart) / (ySplitEnd - ySplitStart)));
            float osc = Mathf.Sin(8f * PI * ((y - ySplitStart) / (ySplitEnd - ySplitStart)));
            //float extra = 40f * envelope * osc; // adding oscilation to the split 

            float cosVal = Mathf.Cos(PI * (y - yCenter) / waveLength);
            float baseX = amplitude * (1f + cosVal);

            //float x1 = xCenter + baseX + extra;
            //float x2 = xCenter - baseX - extra;

            float x1 = xCenter + baseX ;
            float x2 = xCenter - baseX ;


            right.Add(new Vector3(x1, y, 0f));
            left.Add(new Vector3(x2, y, 0f));

        }

        foreach (var point in left) AddTrajectoryPointL(point); // store the left desired trajectory points
        foreach (var point in right) AddTrajectoryPointR(point); // store the right desired trajectory points
        foreach (var point in obs) AddObstacleFlags(point); // store the obstacle flags: 1 for right, -1 for left, 0 for no obstacle
        foreach (var point in obsvis) AddObstacleVisFlags(point); // store the visibility flags of the obstacles: 0 for invisible, 1 for visible to player 1, 2 for visible to player 2, 3 for visible to both players

        isSplit.Add(true); // Split always active in SpawnWave

        //Debug.Log("Obstacle Flags: " + string.Join(", ", WaveSpawner.obstacleFlags));

        return new Vector2(xCenter, ySplitEnd);
    }

    // filling curve -> x = f(y) + g(y), where f is sum of four sine waves and g is a cubic polynomial that ensures smooth connections of the points (x1, y1) and (x2, y2) in the filling curve
    void FillCurveBetween(float x1, float y1, float x2, float y2)
    {
        float step = ySpacing;

        float f1 = GlobalXOffset(y1);
        float f2 = GlobalXOffset(y2);

        float G1 = x1 - f1;
        float G2 = x2 - f2;

        float g1Prime = -GlobalSlope(y1);
        float g2Prime = -GlobalSlope(y2);

        float dy = y2 - y1;
        float dy2 = dy * dy;
        float dy3 = dy2 * dy;

        // Solve cubic coefficients
        float a = (2 * (G1 - G2) + (g1Prime + g2Prime) * dy) / dy3;
        float b = (3 * (G2 - G1) - (2 * g1Prime + g2Prime) * dy) / dy2;
        float c = g1Prime;
        float d = G1;

        for (float y = y1; y <= y2; y += step)
        {
            float t = y - y1;
            float g = a * t * t * t + b * t * t + c * t + d;
            float f = GlobalXOffset(y);
            float x = g + f;

            Vector3 point = new Vector3(x, y, 0f);
            AddTrajectoryPointR(point);
            AddTrajectoryPointL(point);
            AddObstacleFlags(100); // indes of 100 for obstacles in the filling curve
            AddObstacleVisFlags(100); // visibility flag of the filling curve is 100
        }
    }

    // the oscilating component of the filling curve is a sum of four sine waves, which are defined by their amplitudes and wavelengths
    float GlobalXOffset(float y) 
    {
        float offset = 0f;
        for (int i = 0; i < Mathf.Min(sineAmplitudes.Length, sineWavelengths.Length); i++)
        {
            offset += sineAmplitudes[i] * Mathf.Sin(PI * y / sineWavelengths[i]);
            //offset = Mathf.Sin(2.031f * 250f*y) * Mathf.Sin(1.093f*250f * y);
        }
        return offset;
    }

    // calculate the slope of g(y) at a given y
    float GlobalSlope(float y)
    {
        float slope = 0f;
        for (int i = 0; i < Mathf.Min(sineAmplitudes.Length, sineWavelengths.Length); i++)
        {
            slope += sineAmplitudes[i] * PI / sineWavelengths[i] * Mathf.Cos(PI * y / sineWavelengths[i]);
        }
        return slope;
    }

    int CreateObstacle(float x, float y)
    {       
            Vector3 pos = new Vector3(x, y, 0f);
            GameObject o = Instantiate(obstacle, pos, Quaternion.identity);

        // the same obstacles need to be shown to both subjects in lone trials (crtlMode = 1 or 2)
        if (ctrlMode == 1 || ctrlMode == 2)
            {
                float rlone = UnityEngine.Random.value;
                if (rlone < 1 - LoneObstacleProbability)
                {
                    o.layer = LayerMask.NameToLayer("Obstacle_Lone");  // Visible to both players
                    obsvisflag = 3; // visible obstacle for both players
                }

                else
                {
                    o.layer = LayerMask.NameToLayer("Obstacle_Invisable");           // Invisible to both players
                    var sr = o.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.enabled = false;                  // Also hide the sprite
                    var coll = o.GetComponent<Collider2D>();
                    if (coll != null) coll.enabled = false;              // Disable collision if any
                    obsvisflag = 0; // invisible obstacle
                }
                activeObstacles.Add(o); // store the obstacle in the list of active obstacles
                while (ObsFlag.Count < activeObstacles.Count) // initially all obstacles are above the cursor level (y = -200f)
                {
                    ObsFlag.Add(true);
                }
            }


            // the obstacles can be visible to both players in the cooperative trials (ctrlMode = 3 or 4) 
        if (ctrlMode == 3 || ctrlMode == 4)
            {
                // ObstaclesVisibility == 0 means the obstacles are randomly visible to one of the players or invisible to both players with equal probabilies (1/3)

                if (ObstaclesVisibility == 0)
                {
                    float r0 = UnityEngine.Random.value;

                    if (r0 < 1f / 3f)
                    {
                        o.layer = LayerMask.NameToLayer("Obstacle_Player1");  // Visible to Player 1 only
                        obsvisflag = 1; // visible obstacle for Player 1 only
                    }
                    if (1f / 3f <= r0 && 2f / 3f > r0)
                    {
                        o.layer = LayerMask.NameToLayer("Obstacle_Player2");  // Visible to Player 2 only
                        obsvisflag = 2; // visible obstacle for Player 2 only
                    }
                    if (2f / 3f <= r0)
                    {
                        o.layer = LayerMask.NameToLayer("Obstacle_Invisable");           // Invisible to both players
                        var sr = o.GetComponent<SpriteRenderer>();
                        if (sr != null) sr.enabled = false;                  // Also hide the sprite
                        var coll = o.GetComponent<Collider2D>();
                        if (coll != null) coll.enabled = false;              // Disable collision if any
                        obsvisflag = 0; // invisible obstacle for both players
                    }
                    activeObstacles.Add(o); // store the obstacle in the list of active obstacles
                    while (ObsFlag.Count < activeObstacles.Count) // initially all obstacles are above the cursor level (y = -200f)
                    {
                        ObsFlag.Add(true);
                    }
                }

                // ObstaclesVisibility == 1 means the obstacles are visible to Player 1 only with probability ObstacleProbability, and invisible to both players with probability (1 - ObstacleProbability)

                if (ObstaclesVisibility == 1)
                {
                    float r1 = UnityEngine.Random.value;
                    if (r1 < 1 - ObstacleProbability)
                    {
                        o.layer = LayerMask.NameToLayer("Obstacle_Player1");  // Visible to Player 1 only
                        obsvisflag = 1; // visible obstacle for Player 1 only
                    }
                    else
                    {
                        o.layer = LayerMask.NameToLayer("Obstacle_Invisable");           // Invisible to both players
                        var sr = o.GetComponent<SpriteRenderer>();
                        if (sr != null) sr.enabled = false;                  // Also hide the sprite
                        var coll = o.GetComponent<Collider2D>();
                        if (coll != null) coll.enabled = false;              // Disable collision if any
                        obsvisflag = 0; // invisible obstacle
                    }
                    activeObstacles.Add(o); // store the obstacle in the list of active obstacles
                    while (ObsFlag.Count < activeObstacles.Count) // initially all obstacles are above the cursor level (y = -200f)
                    {
                        ObsFlag.Add(true);
                    }
                }

                // ObstaclesVisibility == 2 means the obstacles are visible to Player 2 only with probability ObstacleProbability, and invisible to both players with probability (1 - ObstacleProbability)

                if (ObstaclesVisibility == 2)
                {
                    float r2 = UnityEngine.Random.value;
                    if (r2 < 1 - ObstacleProbability)
                    {
                        o.layer = LayerMask.NameToLayer("Obstacle_Player2");  // Visible to Player 2 only
                        obsvisflag = 2; // visible obstacle for Player 2 only
                    }
                    else
                    {
                        o.layer = LayerMask.NameToLayer("Obstacle_Invisable");          // Invisible to both players
                        var sr = o.GetComponent<SpriteRenderer>();
                        if (sr != null) sr.enabled = false;                  // Also hide the sprite
                        var coll = o.GetComponent<Collider2D>();
                        if (coll != null) coll.enabled = false;              // Disable collision if any
                        obsvisflag = 0; // invisible obstacle
                    }
                    activeObstacles.Add(o); // store the obstacle in the list of active obstacles
                    while (ObsFlag.Count < activeObstacles.Count) // initially all obstacles are above the cursor level (y = -200f)
                    {
                        ObsFlag.Add(true);
                    }
                }
            }
        return obsvisflag; // return the visibility flag of the obstacle
    }

    void AddTrajectoryPointR(Vector3 pos)
    {
        trajectoryPointsR.Add(pos);
    }

    void AddTrajectoryPointL(Vector3 pos)
    {
        trajectoryPointsL.Add(pos);

        while (ReachFlag.Count < trajectoryPointsL.Count) // initilly all trajectory points are above the cursor level (y = -200f)
        {
            ReachFlag.Add(true);
        }
    }

    // Add obstacle flags to the list of obstacle flags:  1 for right, -1 for left, 0 for no obstacle, 100 for the filling curve
    void AddObstacleFlags(int flag)
    {
        ObstacleFlags.Add(flag);
    }

    // Add obstacle visibility flags to the list of obstacle visibility flags: 0 for invisible, 1 for visible to Player 1, 2 for visible to Player 2, 3 for visible to both players
    void AddObstacleVisFlags(int flag) 
    {
        ObstacleVisFlags.Add(flag);
    }


    void DrawLine()
    {
        if (trajectoryPointsR.Count > 0)
        {
            lineRendererR.positionCount = trajectoryPointsR.Count;
            lineRendererR.SetPositions(trajectoryPointsR.ToArray());
        }

        if (trajectoryPointsL.Count > 0)
        {
            lineRendererL.positionCount = trajectoryPointsL.Count;
            lineRendererL.SetPositions(trajectoryPointsL.ToArray());
        }
    }


    void UpdateMesh()
    {
        int pointsCount = trajectoryPointsL.Count;
        if (pointsCount < 2) return; // Not enough points to form a mesh
        float xoffset = 730f;
        float yoffset = 80f;

        // Set the number of vertices
        vertices = new Vector3[pointsCount * 4]; // 4 vertices per trajectory point (2 for left and 2 for right)
        triangles = new int[(pointsCount - 1) * 12]; // 2 triangles per pair of points on each side (left + right)

        // Extend the left and right meshes
        for (int i = 0; i < pointsCount; i++)
        {
            Vector3 leftPoint = trajectoryPointsL[i];
            Vector3 rightPoint = trajectoryPointsR[i];

            // Left side vertices (extend leftwards from the left trajectory point)
            vertices[i * 4] = new Vector3(-1250f, leftPoint.y, 5f); // Left side offscreen (left edge of the screen)
            vertices[i * 4 + 1] = new Vector3(leftPoint.x - marginSize - xoffset, leftPoint.y - yoffset, 5f); // Left trajectory minus margin

            // Right side vertices (extend rightwards from the right trajectory point)
            vertices[i * 4 + 2] = new Vector3(rightPoint.x + marginSize - xoffset, rightPoint.y - yoffset, 5f); // Right trajectory plus margin
            vertices[i * 4 + 3] = new Vector3(0f, rightPoint.y, 5f); // Right side offscreen (right edge of the screen)
        }

        // Create triangles for the left and right mesh regions (two for each pair of points on both sides)
        for (int i = 0; i < pointsCount - 1; i++)
        {
            int startIndex = i * 4;
            int nextIndex = (i + 1) * 4;

            // Left side triangles
            triangles[i * 12] = startIndex;
            triangles[i * 12 + 1] = nextIndex;
            triangles[i * 12 + 2] = startIndex + 1;

            triangles[i * 12 + 3] = nextIndex;
            triangles[i * 12 + 4] = nextIndex + 1;
            triangles[i * 12 + 5] = startIndex + 1;

            // Right side triangles
            triangles[i * 12 + 6] = startIndex + 2;
            triangles[i * 12 + 7] = nextIndex + 2;
            triangles[i * 12 + 8] = startIndex + 3;

            triangles[i * 12 + 9] = nextIndex + 2;
            triangles[i * 12 + 10] = nextIndex + 3;
            triangles[i * 12 + 11] = startIndex + 3;
        }

        // Apply the updated mesh data
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        // Recalculate normals and bounds to improve lighting and collision
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}

