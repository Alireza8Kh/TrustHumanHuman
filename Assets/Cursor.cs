using System.Collections;
using UnityEngine;
using TMPro;

public class Cursor : MonoBehaviour
{
    private Rigidbody cursor;
    public string mvtDeviceName;
    private int ctrlDevice;
    public string scriptCANName;
    private GameObject scriptCAN;
    private float[] robotsState = new float[4];
    private Vector3 posUpdate;
    public const double mathPI = 3.1415926535897931;
    private bool hasFlashed = false;

    void Start()
    {
        cursor = GetComponent<Rigidbody>();
        string deviceSelector = GameObject.Find(mvtDeviceName).GetComponent<TMP_InputField>().text;
        if (deviceSelector == "Key") ctrlDevice = 0;
        else if (deviceSelector == "HRX")
        {
            ctrlDevice = 1;
            scriptCAN = GameObject.Find(scriptCANName);
        }
    }

    void Update()
    {


        if (ctrlDevice == 0)
        {
            float vel_x = Input.GetAxis("Horizontal") * 100;
            Vector3 vel = cursor.velocity;
            vel.x = vel_x;
            cursor.velocity = vel;
        }
        else if (ctrlDevice == 1)
        {
            robotsState[0] = scriptCAN.GetComponent<CANRecorder>().GetCurrentHRXData(0);
            robotsState[1] = 0.0f;
            robotsState[2] = 0.0f;
            robotsState[3] = 0.0f;
            posUpdate.x = -1850.17f / (float)mathPI * robotsState[0] + 296.31f;
            posUpdate.y = cursor.position.y;
            posUpdate.z = cursor.position.z;
            cursor.MovePosition(posUpdate);
        }

    }
}
