using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using System.IO;
using System.Runtime.InteropServices;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.VisualScripting;

public class HandleExpe : MonoBehaviour
{
    // assign the cameras in the inspector for the built and run game
    public Camera[] cameras; // Assign your 4 cameras in Inspector
    private int currentDisplay = 0;

    // to input the obstacle object
    public GameObject obstacle;

    // the margin size
    public static float margin_size = 100f;

    // the master canvas data
    public string cursorName, cursorNameBis;
    public string scriptCANName, ctrlModeSelectName, coKpName, coKdName, mu_vName, subjId1Name, subjId2Name, ObsValName, TrialNumberName;
    public static string TrialNumberN; // to be sent to wavespawner
    public string endBlockTxtName, endBlockTxtNameBis;
    public static int ctrlMode, ObstaclesVisibility;


    // the boolean that becomes true when the master presses the start button
    public bool myBooleanStart;
    public bool StartImportingConsts = true; // this boolean is used to import the game objects and constants only once when the master presses the start button

    // the names of the game objects in the master canvas
    private GameObject cursor, cursorBis;
    private GameObject scriptCAN, ctrlModeSelect, coKpObj, coKdObj, mu_vObj, subjId1, subjId2, ObsVal, TrialNumberObj;
    private GameObject endBlockTxt, endBlockTxtBis;
    private TMP_Text countdownText;

    //lists to store the HRX1 and HRX2 states
    private List<float> rob1PosList = new();
    private List<float> rob1VelList = new();
    private List<float> rob2PosList = new();
    private List<float> rob2VelList = new();

    // store the cursor position of HRX1 and HRX2 accordingly
    private float cursorPos, cursorPosBis;
    // the x of left and right trajectories to be imported from wavespawner
    private float xdR, xdL, obstacle_length;
    public static int FirstobstHit, SecondobstHit; // to send to wavespawner to freeze the game for 2s when the subject hits the obstacle
    private int  FirstMargin, SecondMargin, nbIterations, stopSendConfig;
    private string outPath;
    private float[] robotsState = new float[8];

    public static float relativetime; 
    private float StartTime;
    private List<float> timeList = new();

    // to be sent to CAN to set the control of HRXs
    private float coKp, coKd, mu_v;
    // the Number of the trial from 1 to 5
    public static int TrialNumber; 

    private bool blockEnd, blockEndBis;
    private bool hasStartedCountdown = false;

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
    private const int VK_F5 = 0x74, VK_F6 = 0x75, KEYEVENTF_KEYDOWN = 0x0; // F5 key executes the EMG data recording, F6 key stops it

    void Start()
    {
        // initialize the margin flags
        FirstobstHit = 0;
        SecondobstHit = 0;
        FirstMargin = 0;
        SecondMargin = 0;   

        Renderer rend = obstacle.GetComponent<Renderer>();
        Vector3 visualSize = rend.bounds.size;  // to get the scales of the obstacle -> width and length as x and y of the visualSize
        obstacle_length = visualSize.x;

        // Import all game objects
        ImportGameObjects();

        // Set control mode
        ctrlMode = int.Parse(ctrlModeSelect.GetComponent<TMP_InputField>().text);

        // Set control gains
        coKp = float.Parse(coKpObj.GetComponent<TMP_InputField>().text);
        mu_v = float.Parse(mu_vObj.GetComponent<TMP_InputField>().text);

        // Set configuration sending parameters
        nbIterations = 0;
        stopSendConfig = 15;

        // Check for washout condition
        ObstaclesVisibility = int.Parse(ObsVal.GetComponent<TMP_InputField>().text);

        //
        TrialNumber = int.Parse(TrialNumberObj.GetComponent<TMP_InputField>().text);


        // Build paths for data saving
        string folderpath = "./TrustExpe/raw_data/S" + subjId1.GetComponent<TMP_InputField>().text + "S" + subjId2.GetComponent<TMP_InputField>().text + "/";
        if (ctrlMode == 1)
        {
            outPath = folderpath + "TrustExpe_S" + subjId1.GetComponent<TMP_InputField>().text + "S" + subjId2.GetComponent<TMP_InputField>().text
                    + "_Ctrl_1_Kp_0" + "_TrialNum_" + TrialNumberObj.GetComponent<TMP_InputField>().text + "_ObsVis_" + ObsVal.GetComponent<TMP_InputField>().text + ".csv";
        }
        else if (ctrlMode == 2)
        {
            outPath = folderpath + "Failure.csv";
        }
        else if (ctrlMode == 3)
        {
            outPath = folderpath + "TrustExpe_S" + subjId1.GetComponent<TMP_InputField>().text + "S" + subjId2.GetComponent<TMP_InputField>().text
                    + "_Ctrl_3_Kp_" + coKp.ToString() + "_TrialNum_" + TrialNumberObj.GetComponent<TMP_InputField>().text + "_ObsVis_" + ObsVal.GetComponent<TMP_InputField>().text + ".csv";
        }
        else
        {
            outPath = folderpath + "Failure.csv";
        }

        // Initialize output folders and files
        if (!Directory.Exists(folderpath)) { Directory.CreateDirectory(folderpath); }
        if (File.Exists(outPath)) { File.Delete(outPath); }
        StartImportingConsts = false; // prevent re-importing game objects and constants

        if (nbIterations < stopSendConfig)
        {
            scriptCAN.GetComponent<CANRecorder>().SendCtrlConfig(ctrlMode, mu_v, coKp, coKd, FirstobstHit, SecondobstHit, FirstMargin, SecondMargin); // send the conntrol parameters to CAN bus -> to the robots
            nbIterations++;
        }

        // trigger the countdown when master presses start

        countdownText = GameObject.Find("CountdownText").GetComponent<TMP_Text>();

        // Initialize end of block flags
        blockEnd = false;
        blockEndBis = false;
    }

    private void ImportGameObjects()
    {
        // Impoert HRX1 cursor and end block text
        cursor = GameObject.Find(cursorName);
        endBlockTxt = GameObject.Find(endBlockTxtName);

        // Import HRX2 cursor and end block text
        cursorBis = GameObject.Find(cursorNameBis);
        endBlockTxtBis = GameObject.Find(endBlockTxtNameBis);

        // Import other game objects from the master canvas
        scriptCAN = GameObject.Find(scriptCANName);
        ctrlModeSelect = GameObject.Find(ctrlModeSelectName);
        coKpObj = GameObject.Find(coKpName);
        coKdObj = GameObject.Find(coKdName);
        mu_vObj = GameObject.Find(mu_vName);
        subjId1 = GameObject.Find(subjId1Name);
        subjId2 = GameObject.Find(subjId2Name);
        ObsVal = GameObject.Find(ObsValName);
        TrialNumberObj = GameObject.Find(TrialNumberName);
    }


    // This method starts the countdown and then begins the experiment.
    private IEnumerator StartCountdownThenBegin()
    {
        if (countdownText == null)
        {
            Debug.LogError("Countdown Text not assigned.");
            yield break;
        }

        countdownText.enabled = true;

        countdownText.text = "3";
        yield return new WaitForSeconds(1f);
        countdownText.text = "2";
        yield return new WaitForSeconds(1f);
        countdownText.text = "1";
        yield return new WaitForSeconds(1f);
        countdownText.text = "GO!";
        yield return new WaitForSeconds(0.5f);

        countdownText.enabled = false;
        myBooleanStart = true; // starts recording data

        keybd_event(VK_F5, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero); // virtually press F5 to start EMG recording
        StartTime = Time.time;
        WaveSpawner.StartTime = StartTime; // set the start time of the wave spawner
        relativetime = 0f; // reset the relative time
        Debug.Log("Experiment started. EMG recording started, wave spawner absolute start time set to: " + WaveSpawner.StartTime);
    }

    void Update()
    {
        relativetime = Time.time - StartTime; // calculate the relative time since the start of the experiment
        if (myBooleanStart)
        {
            float pos1 = robotsState[0];
            float pos2 = robotsState[1];
            float vel1 = robotsState[2];
            float vel2 = robotsState[3];
            float torque1 = robotsState[4];
            float torque2 = robotsState[5];
            float posAvg = (cursorPos + cursorPosBis) / 2f;

            // import the cursor position from cursor objects
            cursorPos = cursor.transform.position.x;
            cursorPosBis = cursorBis.transform.position.x;

            // import xdl and xdr from wavespawner
            xdR = WaveSpawner.xdR; // desired right trajectory imported from wavespawner
            xdL = WaveSpawner.xdL; // desired left trajectory imported from wavespawner

            // reset the margin flags at the beginning of each update
            FirstMargin = 0;
            SecondMargin = 0;

            // check if subjects go into the margin
            if (cursorPos < xdL - margin_size || cursorPos > xdR + margin_size)
                FirstMargin = 1;
            if (cursorPosBis < xdL - margin_size || cursorPosBis > xdR + margin_size)
                SecondMargin = 1;


                for (int i = 0; i < 8; i++)
                robotsState[i] = scriptCAN.GetComponent<CANRecorder>().GetCurrentHRXData(i);

            FirstobstHit = 0; // reset the first subject's obstacle hit flags
            SecondobstHit = 0; // reset the second subject's obstacle hit flags

            if (ctrlMode == 1 || ctrlMode == 2)
            {
                Flash1(cursorPos);
                Flash2(cursorPosBis);
            }
            else if (ctrlMode == 3 || ctrlMode == 4)
            {
                CheckAndFlashIfNearObstacleCoupled();
            }

            // Save the state of the robots
            WriteStateToFiles(pos1, pos2, vel1, vel2, torque1, torque2, posAvg, FirstobstHit, SecondobstHit, FirstMargin, SecondMargin);
            scriptCAN.GetComponent<CANRecorder>().SendCtrlConfig(ctrlMode, mu_v, coKp, coKd, FirstobstHit, SecondobstHit, FirstMargin, SecondMargin);
            //Debug.Log(relativetime + " " + FirstMargin + "\n");
        }
    }


    // This method checks if the first subject's cursor is near an obstacle (at the same time and the position) in the uncoupled blocks (ctrlMode = 1 or 2), and flashes the first subjects's screen if it is.
    private void Flash1(float cursorpos)
    {
        float currentTime = relativetime;

        for (int i = 0; i < WaveSpawner.obstacleHitTimes.Count; i++)
        {
            float targetTime = WaveSpawner.obstacleHitTimes[i];
            Vector3 obsPos = WaveSpawner.obstaclePositions[i];
            int obstacleLayer = WaveSpawner.activeObstacles[i].gameObject.layer; // get the layer of the obstacle from wavespawner
            // obstacleLayer = 6 -> obstacle visible for subject 1
            // obstacleLayer = 7 -> obstacle visible for subject 2
            // obstacleLayer = 13 -> obstacle invisible for both subjects
            // obstacleLayer = 14 -> same obstacles visible for both subjects (Lone blocks), but in different layers (14 for subject 1, 15 for subject 2)
            // obstacleLayer = 15 -> same obstacles visible for both subjects (Lone blocks), but in different layers (14 for subject 1, 15 for subject 2)

            if (currentTime > targetTime && currentTime < targetTime + 0.1f)
            {
                float dx1 = Mathf.Abs(cursorpos - obsPos.x);

                // check if the firt subject's cursors is close to the obstacle, and if so, flash the screen. The obstacle is visible for subject 1 if its layer is 6.

                if (dx1 < 0.5f* obstacle_length && obstacleLayer == 14)
                {
                    FirstobstHit = 1;
                    Subject1ScreenFlash flash = FindObjectOfType<Subject1ScreenFlash>();
                    if (flash != null) flash.Subject1Flash();
                }
                else
                {
                    FirstobstHit = 0;
                }          
            }
        }
    }

    // This method checks if the second subject's cursor is near an obstacle (at the same time and the position) in the uncoupled blocks (ctrlMode = 1 or 2), and flashes the second subjects's screen if it is.

    private void Flash2(float cursorPosBis)
    {
        float currentTime = relativetime;

        for (int i = 0; i < WaveSpawner.obstacleHitTimes.Count; i++)
        {
            float targetTime = WaveSpawner.obstacleHitTimes[i];
            Vector3 obsPos = WaveSpawner.obstaclePositions[i];
            int obstacleLayer = WaveSpawner.activeObstacles[i].gameObject.layer; // get the layer of the obstacle from wavespawner
            // obstacleLayer = 6 -> obstacle visible for subject 1
            // obstacleLayer = 7 -> obstacle visible for subject 2
            // obstacleLayer = 13 -> obstacle invisible for both subjects
            // obstacleLayer = 14 -> same obstacles visible for both subjects (Lone blocks), but in different layers (14 for subject 1, 15 for subject 2)
            // obstacleLayer = 15 -> same obstacles visible for both subjects (Lone blocks), but in different layers (14 for subject 1, 15 for subject 2)

            if (currentTime > targetTime && currentTime < targetTime + 0.1f)
            {
                float dx2 = Mathf.Abs(cursorPosBis - obsPos.x);

                // check if the second subject's cursors is close to the obstacle, and if so, flash the screen

                if (dx2 < 0.5f * obstacle_length && obstacleLayer == 14)
                {
                    SecondobstHit = 1;
                    Subject2ScreenFlash flash = FindObjectOfType<Subject2ScreenFlash>();
                    if (flash != null) flash.Subject2Flash();
                }
                else
                {
                    SecondobstHit = 0;
                }
            }
        }
    }

    // This method checks if the cursor is near an obstacle (at the same time and the position) in the coupled blocks (ctrlMode = 3 or 4), and flashes the screen if it is.
    private void CheckAndFlashIfNearObstacleCoupled()
    {
        float currentTime = relativetime;

        for (int i = 0; i < WaveSpawner.obstacleHitTimes.Count; i++)
        {
            float targetTime = WaveSpawner.obstacleHitTimes[i];
            Vector3 obsPos = WaveSpawner.obstaclePositions[i];
            int obstacleLayer = WaveSpawner.activeObstacles[i].gameObject.layer; // get the layer of the obstacle from wavespawner

            // obstacleLayer = 6 -> obstacle visible for subject 1
            // obstacleLayer = 7 -> obstacle visible for subject 2
            // obstacleLayer = 13 -> obstacle invisible for both subjects
            // obstacleLayer = 14 -> same obstacles visible for both subjects (Lone blocks), but in different layers (14 for subject 1, 15 for subject 2)
            // obstacleLayer = 15 -> same obstacles visible for both subjects (Lone blocks), but in different layers (14 for subject 1, 15 for subject 2)

            if (currentTime > targetTime && currentTime < targetTime + 0.1f)
            {
                float dx1 = Mathf.Abs(cursor.transform.position.x - obsPos.x);
                float dx2 = Mathf.Abs(cursorBis.transform.position.x - obsPos.x);

                // check if the firt subject's cursors is close to the obstacle, and if so, flash the screen

                if ((dx1 < 0.5f * obstacle_length || dx2 < 0.5f * obstacle_length) && obstacleLayer == 6)
                {
                    FirstobstHit = 1;
                    SecondobstHit = 1;
                    Subject1ScreenFlash flash = FindObjectOfType<Subject1ScreenFlash>();
                    Subject2ScreenFlash flash2 = FindObjectOfType<Subject2ScreenFlash>();
                    if (flash || flash2 != null)
                    {
                        flash.Subject1Flash();
                        flash2.Subject2Flash();
                    }
                }
                else
                {
                    FirstobstHit = 0;
                }

                // check if the second subject's cursors is close to the obstacle, and if so, flash the screen

                if ((dx1 < 0.5f * obstacle_length || dx2 < 0.5f * obstacle_length) && obstacleLayer == 7)
                {
                    SecondobstHit = 1;
                    SecondobstHit = 1;
                    Subject1ScreenFlash flash = FindObjectOfType<Subject1ScreenFlash>();
                    Subject2ScreenFlash flash2 = FindObjectOfType<Subject2ScreenFlash>();
                    if (flash || flash2 != null)
                    {
                        flash.Subject1Flash();
                        flash2.Subject2Flash();
                    }
                }
                else
                {
                    SecondobstHit = 0;
                }
            }
        }
    }

    // This method updates the active camera based on the current display index. Right arrow key switches to the next camera, left arrow key switches to the previous camera.
    void UpdateActiveCamera()
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].gameObject.SetActive(i == currentDisplay);
        }
        Debug.Log("Switched to display: " + currentDisplay);
    }

    // This method writes the state of the experiment to a file.
    private void WriteStateToFiles(float pos1, float pos2, float vel1, float vel2, float torque1, float torque2, float posAvg, int FirstobstHit, int SecondobstHit, int FirstMargin, int SecondMargin)
    {
        string delimiter = ",";


        float[] outputToWrite = new float[] {relativetime,cursorPos, pos1, vel1, torque1, cursorPosBis, pos2, vel2, torque2, posAvg, xdR, xdL, WaveSpawner.VisObs,WaveSpawner.LiveObs, FirstobstHit, SecondobstHit};

        StringBuilder sbOutput = new StringBuilder();
        string[] currentState = new string[outputToWrite.Length];
        for (int i = 0; i < outputToWrite.Length; i++)
            currentState[i] = outputToWrite[i].ToString();

        sbOutput.AppendLine(string.Join(delimiter, currentState));

        if (!File.Exists(outPath))
            File.WriteAllText(outPath, sbOutput.ToString());
        else
            File.AppendAllText(outPath, sbOutput.ToString());

        //Debug.Log("Logged obstacleFlag: " + obstacleFlag);
    }

    // this method triggers the countdown only once when the master presses the start button
    public void startBlock()
    {
        if (!hasStartedCountdown)
        {
            StartCoroutine(StartCountdownThenBegin());
            hasStartedCountdown = true;
        }
    }

    // this method is called when the master presses the stop button, it stops the game and the data recording
    public void StopGame()
    {
        myBooleanStart = false;
        Debug.Log("Game stopped.");
        // Simulate F6 key press
        keybd_event(VK_F6, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
        scriptCAN.GetComponent<CANRecorder>().SendCtrlConfig(ctrlMode, mu_v, coKp, coKd, 0, 0, 0, 0);
        Application.Quit(); // close the application
    }
}
