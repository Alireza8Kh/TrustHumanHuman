using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CursorHRX2 : MonoBehaviour
{
    // Rigid body associated with cursor
    private Rigidbody cursor;
    // Elements for control device selection
    public string mvtDeviceName;
    private int ctrlDevice;
    // Elements to perform HRX-based movement control
    public string scriptCANName; private GameObject scriptCAN;
    private float[] robotsState = new float[4] { 0.0f, 0.0f, 0.0f, 0.0f }; private Vector3 posUpdate;
    public const double mathPI = 3.1415926535897931;
    // Start is called before the first frame update
    void Start()
    {
        // Get cursor object
        cursor = GetComponent<Rigidbody>();
        // Extract the device controlling the cursor
        string deviceSelector = GameObject.Find(mvtDeviceName).GetComponent<TMP_InputField>().text;
        // Assign the local selector and import required elements
        if (deviceSelector == "Key") { ctrlDevice = 0; }
        else if (deviceSelector == "HRX")
        {
            // Assignment
            ctrlDevice = 1;
            // Extract script to update cursor position
            scriptCAN = GameObject.Find(scriptCANName);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Control cursor with the defined device
        if (ctrlDevice == 0)
        {
            // Get inputs from keyboard -> Will be modified to allow movement based on HRX
            float vel_x = Input.GetAxis("Horizontal") * 100;
            // Set cursor velocity
            Vector3 vel = cursor.velocity;
            vel.x = vel_x;
            cursor.velocity = vel;
        }
        else if (ctrlDevice == 1)
        {
            // Get status of the HRX robots
            robotsState[0] = 0.0f;
            robotsState[1] = scriptCAN.GetComponent<CANRecorder>().GetCurrentHRXData(1);
            robotsState[2] = 0.0f;
            robotsState[3] = 0.0f;
            // Compute next position (relation identified with robot completely leftwards shen turned on)
            posUpdate.x = -1850.17f / (float)mathPI * robotsState[1] + 296.31f;
            posUpdate.y = cursor.position.y;
            posUpdate.z = cursor.position.z;
            // Apply position
            cursor.MovePosition(posUpdate);
        }
    }
}
