using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

using Peak.Can.Basic;
using TPCANHandle = System.UInt16;
using TPCANTimestampFD = System.UInt64;
//using UnityEngine.InputSystem;

public class CANRecorder : MonoBehaviour
{
    // Number of messages to check
    private int ChannelCount = 10;
    // Lists of names for the messages
    private List<string> IDNames = new List<string> { "Position1", "Torque1", "Velocity1", "Position2", "Torque2", "Velocity2", "FirstobsHit", "SecondobsHit", "FirstMargin", "SecondMargin" };
    // Lists and identifiers for the messages
    private List<uint> CANIDs = new List<uint> { 0x381, 0x281, 0x481, 0x382, 0x282, 0x482, 0x66A, 0x66B, 0x66C, 0x66D };
    // Set length of the messages (4 bytes for position messages; 2 bytes for torque messages)
    private List<int> ByteLengths = new List<int> { 4, 2, 4, 4, 2, 4, 2, 2, 2, 2 };
    // Output after reading CAN
    private byte[] CANData;
    // Build dictionnary to extract messages
    private Dictionary<uint, Tuple<string, int>> CANinfo = new Dictionary<uint, Tuple<string, int>>();
    // Variables to store positions and torques
    private float pos1 = 0.0f, pos2 = 0.0f, vel1 = 0.0f, vel2 = 0.0f, torque1 = 0.0f, torque2 = 0.0f;

    void Start()
    {
        // Build dictionary to use CAN channels
        CANinfo.Clear();
        for (int i = 0; i < ChannelCount; i += 1)
        {
            CANinfo.Add(CANIDs[i], Tuple.Create(IDNames[i], ByteLengths[i]));
        }
    }
    private void FixedUpdate()
    {
        // Pull all the messages in the dictionary, regardless of it being updated or not
        foreach (uint id in CANIDs)
        {
            int data = 0;
            float angle = 0.0f;
            float vel = 0.0f;
            float torque = 0.0f;

            if (CanController.GetCANMessageData(id, out CANData))
            {
                //Debug.Log("Message with ID" + id.ToString());
                if (CANinfo[id].Item2 == 4)
                {
                    // Get the data
                    data = BitConverter.ToInt32(CANData, 0);
                    // Convert to usable units
                    if (id == 0x381 || id == 0x382)
                    {
                        angle = (float)data * 2 * Mathf.PI / 25600; // [rad]
                    }
                    else if (id == 0x481 || id == 0x482)
                    {
                        vel = (float)data * 2f * Mathf.PI / 60f; // [rad/s]
                        //Debug.Log("Vel: " + vel.ToString() + " Message: " + id.ToString());
                    }
                }
                if (CANinfo[id].Item2 == 2)
                {
                    data = BitConverter.ToInt16(CANData, 0);
                    torque = (float)data * 1.0f / 1000f;
                }
                // Save data to variables
                if (id == 0x381) { pos1 = angle + 1.994f; }
                else if (id == 0x382) { pos2 = angle + 1.835f; }
                if (id == 0x481) { vel1 = vel; }
                else if (id == 0x482) { vel2 = vel; }
                else if (id == 0x281) { torque1 = torque; }
                else if (id == 0x282) { torque2 = torque; }
            }
            // If no data available (i.e. transmission error)
            else
            {
                //Debug.Log("Couldn't find data with ID" + id.ToString());
                //Debug.Log("Data" + CANData.ToString());
            }
        }
    }

    public float GetCurrentHRXData(int reqData)
    {
        if (reqData == 0) { return pos1; }
        else if (reqData == 1) { return pos2; }
        else if (reqData == 2) { return vel1; }
        else if (reqData == 3) { return vel2; }
        else if (reqData == 4) { return torque1; }
        else if (reqData == 5) { return torque2; }
        else { return -1; }
    }

    //public void SendTorqueOrders(int robotId, float torqueOrder)
    //{
    //    // Transform order in integer mNm
    //    int torque_mNm = (int)(torqueOrder * 1000f);
    //    // Convert order to bytes for transmission
    //    Int16 dataInt16 = Convert.ToInt16(Convert.ToSingle(torque_mNm));
    //    byte[] data = BitConverter.GetBytes(dataInt16);
    //    // Send order
    //    if (robotId == 1)
    //    {
    //        CanController.SendCANMessage(0x303, 2, data);
    //    }
    //    else
    //    {
    //        CanController.SendCANMessage(0x304, 2, data);
    //    }
    //}

    public void SendCtrlConfig(int ctrlMode, float mu_v, float coKp, float coKd, int FirstobsHit, int SecondobsHit, int FirstMargin, int SecondMargin)
    {
        // Transform all floats to integers
        int mu_v_int = (int)(mu_v * 1000f);
        int coKp_int = (int)(coKp * 1000f);
        int coKd_int = (int)(coKd * 1000f);

        // Convert order to bytes for transmission
        // Control mode
        Int16 ctrlMode_Int16 = Convert.ToInt16(Convert.ToSingle(ctrlMode));
        byte[] ctrlMode_Byte = BitConverter.GetBytes(ctrlMode_Int16);
        // Viscosity: mu_v
        Int16 mu_v_Int16 = Convert.ToInt16(Convert.ToSingle(mu_v_int));
        byte[] mu_v_Byte = BitConverter.GetBytes(mu_v_Int16);
        // Connection Stiffness: coKp
        Int16 coKp_Int16 = Convert.ToInt16(Convert.ToSingle(coKp_int));
        byte[] coKp_Byte = BitConverter.GetBytes(coKp_Int16);
        // Connection Stiffness: coKp
        Int16 coKd_Int16 = Convert.ToInt16(Convert.ToSingle(coKd_int));
        byte[] coKd_Byte = BitConverter.GetBytes(coKd_Int16);
        // First subject hits the obstacle
        Int16 FirstobsHit_Int16 = Convert.ToInt16(Convert.ToSingle(FirstobsHit));
        byte[] FirstobsHit_Byte = BitConverter.GetBytes(FirstobsHit_Int16);
        // Second subject hits the obstacle
        Int16 SecondobsHit_Int16 = Convert.ToInt16(Convert.ToSingle(SecondobsHit));
        byte[] SecondobsHit_Byte = BitConverter.GetBytes(SecondobsHit_Int16);
        // First subject goes into the margin
        Int16 FirstMargin_Int16 = Convert.ToInt16(Convert.ToSingle(FirstMargin));
        byte[] FirstMargin_Byte = BitConverter.GetBytes(FirstMargin_Int16);
        // Second subject goes into the margin 
        Int16 SecondMargin_Int16 = Convert.ToInt16(Convert.ToSingle(SecondMargin));
        byte[] SecondMargin_Byte = BitConverter.GetBytes(SecondMargin_Int16);

        // Send all parameters of config
        CanController.SendCANMessage(0x666, 2, ctrlMode_Byte);
        CanController.SendCANMessage(0x667, 2, mu_v_Byte);
        CanController.SendCANMessage(0x668, 2, coKp_Byte);
        CanController.SendCANMessage(0x669, 2, coKd_Byte);
        CanController.SendCANMessage(0x66A, 2, FirstobsHit_Byte);
        CanController.SendCANMessage(0x66B, 2, SecondobsHit_Byte);
        CanController.SendCANMessage(0x66C, 2, FirstMargin_Byte);
        CanController.SendCANMessage(0x66D, 2, SecondMargin_Byte);
    }
}


