using System;
using UnityEngine;
using System.Threading;
using Peak.Can.Basic;
using System.Collections.Generic;
using TPCANHandle = System.UInt16;
using TPCANTimestampFD = System.UInt64;

public class CanController : MonoBehaviour
{
    private TPCANHandle PCANhandle = 0;

    private bool m_DLLFound;
    private Thread m_ReadThread;
    private bool m_ThreadRun;

    private static IDictionary<uint, TPCANMsg> inCANMessages = new Dictionary<uint, TPCANMsg>();
    private static IDictionary<uint, TPCANMsg> outCANMessages = new Dictionary<uint, TPCANMsg>();


    // Awake is called when an enabled script instance is being loaded
    void Awake()
    {
        Time.fixedDeltaTime = 0.001f;

        // Checks if PCANBasic.dll is available, if not, the program terminates
        m_DLLFound = CheckForLibrary();
        if (!m_DLLFound)
            return;

        PCANhandle = GetPCANHandle();
        if (PCANhandle == 0)
            return;

        TPCANStatus PCANStatus = PCANBasic.Initialize(PCANhandle, TPCANBaudrate.PCAN_BAUD_1M);
        if (PCANStatus != TPCANStatus.PCAN_ERROR_OK)
        {
            Debug.Log("PCAN Initialize Failed");
            Debug.Log(GetFormattedError(PCANStatus));
            return;
        }

        //inCANMessages = new Dictionary<uint, TPCANMsg>();
        //outCANMessages = new Dictionary<uint, TPCANMsg>();

        // Start reader
        m_ReadThread = new Thread(new ThreadStart(ThreadExecute));
        m_ThreadRun = true;
        m_ReadThread.Start();

    }

    // Update is called once per frame
    void Update()
    {
        //PrintinCANMessages();
    }

    void FixedUpdate()
    {
        SendCANMessages();
    }

    public static bool GetCANMessageData(uint ID, out byte[] data)
    {
        data = new byte[] { };
        if (inCANMessages.ContainsKey(ID))
        {
            data = inCANMessages[ID].DATA;
            return true;
        }

        //Debug.Log("INVALID ID " + ID);
        return false;
    }

    // Adds message to outgoing queue
    public static void SendCANMessage(uint CanID, byte len, byte[] data)
    {
        var msgCanMessage = new TPCANMsg();
        msgCanMessage.DATA = data;
        msgCanMessage.ID = CanID;
        msgCanMessage.LEN = len; // If len < data then message crops data until len
        msgCanMessage.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD;

        outCANMessages.Add(CanID, msgCanMessage);
    }

    private void SendCANMessages()
    {
        foreach (var kvp in outCANMessages)
        {
            WriteCANMessage(kvp.Value);
        }
        outCANMessages.Clear();
    }

    private void WriteCANMessage(TPCANMsg msgCanMessage)
    {

        TPCANStatus status = PCANBasic.Write(PCANhandle, ref msgCanMessage);

        if (status != TPCANStatus.PCAN_ERROR_OK)
        {
            Debug.Log("Error Sending CAN Message");
            Debug.Log(GetFormattedError(status));
            return;
        }
    }


    private void ThreadExecute()
    {
        // Sets the handle of the Receive-Event.
        AutoResetEvent evtReceiveEvent = new AutoResetEvent(false);
        UInt32 iBuffer = Convert.ToUInt32(evtReceiveEvent.SafeWaitHandle.DangerousGetHandle().ToInt32());
        TPCANStatus stsResult = PCANBasic.SetValue(PCANhandle, TPCANParameter.PCAN_RECEIVE_EVENT, ref iBuffer, sizeof(UInt32));

        if (stsResult != TPCANStatus.PCAN_ERROR_OK)
        {
            ShowStatus(stsResult);
            return;
        }

        while (m_ThreadRun)
            // Checks for messages when an event is received
            if (evtReceiveEvent.WaitOne(50))
                ReadMessages();

        // Removes the Receive-Event again.
        iBuffer = 0;
        stsResult = PCANBasic.SetValue(PCANhandle, TPCANParameter.PCAN_RECEIVE_EVENT, ref iBuffer, sizeof(UInt32));

        if (stsResult != TPCANStatus.PCAN_ERROR_OK)
            ShowStatus(stsResult);
        evtReceiveEvent.Dispose();
    }

    private void ReadMessages()
    {
        TPCANStatus stsResult;
        // We read at least one time the queue looking for messages. If a message is found, we look again trying to 
        // find more. If the queue is empty or an error occurr, we get out from the dowhile statement.
        do
        {
            stsResult = ReadMessage();
            if (stsResult != TPCANStatus.PCAN_ERROR_OK && stsResult != TPCANStatus.PCAN_ERROR_QRCVEMPTY)
            {
                ShowStatus(stsResult);
                return;
            }
        } while ((!Convert.ToBoolean(stsResult & TPCANStatus.PCAN_ERROR_QRCVEMPTY)));
    }

    private TPCANStatus ReadMessage()
    {
        // We execute the "Read" function of the PCANBasic     
        TPCANStatus stsResult = PCANBasic.Read(PCANhandle, out TPCANMsg CANMsg, out TPCANTimestamp CANTimeStamp);
        if (stsResult != TPCANStatus.PCAN_ERROR_QRCVEMPTY)
            // We process the received message
            ProcessMessageCan(CANMsg, CANTimeStamp);

        return stsResult;
    }

    private void ProcessMessageCan(TPCANMsg msg, TPCANTimestamp itsTimeStamp)
    {
        ulong microsTimestamp = itsTimeStamp.micros + (1000UL * itsTimeStamp.millis) + (0x100_000_000UL * 1000UL * itsTimeStamp.millis_overflow);


        if (inCANMessages.ContainsKey(msg.ID))
        {
            inCANMessages[msg.ID] = msg;
        }
        else
        {
            inCANMessages.Add(msg.ID, msg);
        }


        //Debug.Log("Type: " + GetMsgTypeString(msg.MSGTYPE));
        //Debug.Log("ID: " + GetIdString(msg.ID, msg.MSGTYPE));
        //Debug.Log("Length: " + msg.LEN.ToString());
        //Debug.Log("Time: " + GetTimeString(microsTimestamp));
        //Debug.Log("Data: " + GetDataString(msg.DATA, msg.MSGTYPE, msg.LEN));
        //Debug.Log("----------------------------------------------------------");
    }

    private void PrintinCANMessages()
    {
        Debug.Log("CAN Message list");
        foreach (var keyValuePair in inCANMessages)
        {
            TPCANMsg msg = keyValuePair.Value;
            //Debug.Log("ID: " + GetIdString(msg.ID, msg.MSGTYPE) + " Data: " + GetDataString(msg.DATA, msg.MSGTYPE, msg.LEN));
        }
    }

    private TPCANHandle GetPCANHandle()
    {

        const string DeviceType = "PCAN_USB";
        //const string DeviceID = "";
        //const string ControllerNumber = "";
        //const string IPAddress = "";

        string sParameters = "";
        if (DeviceType != "")
        {
            sParameters += PCANBasic.LOOKUP_DEVICE_TYPE + "=" + DeviceType;
        }
        //if (DeviceID != "")
        //{
        //    sParameters += ", " + PCANBasic.LOOKUP_DEVICE_ID + "=" + DeviceID;
        //}
        //if (ControllerNumber != "")
        //{
        //    sParameters += ", " + PCANBasic.LOOKUP_CONTROLLER_NUMBER + "=" + ControllerNumber;
        //}
        //if (IPAddress != "")
        //{
        //    sParameters += ", " + PCANBasic.LOOKUP_IP_ADDRESS + "=" + IPAddress;
        //}

        TPCANStatus stsResult = PCANBasic.LookUpChannel(sParameters, out TPCANHandle handle);

        if (stsResult == TPCANStatus.PCAN_ERROR_OK)
        {

            if (handle != PCANBasic.PCAN_NONEBUS)
            {
                stsResult = PCANBasic.GetValue(handle, TPCANParameter.PCAN_CHANNEL_FEATURES, out uint iFeatures, sizeof(uint));

                if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                {
                    Debug.Log("The channel handle " + FormatChannelName(handle, (iFeatures & PCANBasic.FEATURE_FD_CAPABLE) == PCANBasic.FEATURE_FD_CAPABLE) + " was found");
                    return handle;
                }
                else
                    Debug.Log("There was an issue retrieveing supported channel features");
            }
            else
                Debug.Log("A handle for these lookup-criteria was not found");
        }

        if (stsResult != TPCANStatus.PCAN_ERROR_OK)
        {
            Debug.Log("There was an error looking up the device, are any hardware channels attached?");
        }

        return 0;
    }

    private string FormatChannelName(TPCANHandle handle, bool isFD)
    {
        TPCANDevice devDevice;
        byte byChannel;

        // Gets the owner device and channel for a PCAN-Basic handle
        if (handle < 0x100)
        {
            devDevice = (TPCANDevice)(handle >> 4);
            byChannel = (byte)(handle & 0xF);
        }
        else
        {
            devDevice = (TPCANDevice)(handle >> 8);
            byChannel = (byte)(handle & 0xFF);
        }

        // Constructs the PCAN-Basic Channel name and return it
        if (isFD)
            return string.Format("{0}:FD {1} ({2:X2}h)", devDevice, byChannel, handle);

        return string.Format("{0} {1} ({2:X2}h)", devDevice, byChannel, handle);
    }

    private string GetFormattedError(TPCANStatus error)
    {
        // Creates a buffer big enough for a error-text
        var strTemp = new System.Text.StringBuilder(256);
        // Gets the text using the GetErrorText API function. If the function success, the translated error is returned. 
        // If it fails, a text describing the current error is returned.
        if (PCANBasic.GetErrorText(error, 0x09, strTemp) != TPCANStatus.PCAN_ERROR_OK)
            return string.Format("An error occurred. Error-code's text ({0:X}) couldn't be retrieved", error);
        return strTemp.ToString();
    }

    private bool CheckForLibrary()
    {
        // Check for dll file
        try
        {
            Debug.Log("Looking for library.");
            PCANBasic.Uninitialize(PCANBasic.PCAN_NONEBUS);
            return true;
        }
        catch (DllNotFoundException)
        {
            Debug.Log("Unable to find the library: PCANBasic.dll !");

        }
        return false;
    }

    private void ShowStatus(TPCANStatus status)
    {
        Debug.Log("=========================================================================================");
        Debug.Log(GetFormattedError(status));
        Debug.Log("=========================================================================================");
    }

    private string GetMsgTypeString(TPCANMessageType msgType)
    {
        if ((msgType & TPCANMessageType.PCAN_MESSAGE_STATUS) == TPCANMessageType.PCAN_MESSAGE_STATUS)
            return "STATUS";

        if ((msgType & TPCANMessageType.PCAN_MESSAGE_ERRFRAME) == TPCANMessageType.PCAN_MESSAGE_ERRFRAME)
            return "ERROR";

        string strTemp;
        if ((msgType & TPCANMessageType.PCAN_MESSAGE_EXTENDED) == TPCANMessageType.PCAN_MESSAGE_EXTENDED)
            strTemp = "EXT";
        else
            strTemp = "STD";

        if ((msgType & TPCANMessageType.PCAN_MESSAGE_RTR) == TPCANMessageType.PCAN_MESSAGE_RTR)
            strTemp += "/RTR";
        else
            if ((int)msgType > (int)TPCANMessageType.PCAN_MESSAGE_EXTENDED)
        {
            strTemp += " [ ";
            if ((msgType & TPCANMessageType.PCAN_MESSAGE_FD) == TPCANMessageType.PCAN_MESSAGE_FD)
                strTemp += " FD";
            if ((msgType & TPCANMessageType.PCAN_MESSAGE_BRS) == TPCANMessageType.PCAN_MESSAGE_BRS)
                strTemp += " BRS";
            if ((msgType & TPCANMessageType.PCAN_MESSAGE_ESI) == TPCANMessageType.PCAN_MESSAGE_ESI)
                strTemp += " ESI";
            strTemp += " ]";
        }

        return strTemp;
    }

    private string GetIdString(uint id, TPCANMessageType msgType)
    {
        if ((msgType & TPCANMessageType.PCAN_MESSAGE_EXTENDED) == TPCANMessageType.PCAN_MESSAGE_EXTENDED)
            return string.Format("{0:X8}h", id);

        return string.Format("{0:X3}h", id);
    }

    private string GetTimeString(TPCANTimestampFD time)
    {
        double fTime = (time / 1000.0);
        return fTime.ToString("F1");
    }

    /// <summary>
    /// Gets the data of a CAN message as a string
    /// </summary>
    /// <param name="data">Array of bytes containing the data to parse</param>
    /// <param name="msgType">Type flags of the message the data belong</param>
    /// <param name="dataLength">The amount of bytes to take into account wihtin the given data</param>
    /// <returns>A string with hexadecimal formatted data bytes of a CAN message</returns>
    private string GetDataString(byte[] data, TPCANMessageType msgType, int dataLength)
    {
        string strTemp = "";

        if ((msgType & TPCANMessageType.PCAN_MESSAGE_RTR) == TPCANMessageType.PCAN_MESSAGE_RTR)
            return "Remote Request";
        else
            for (int i = 0; i < dataLength; i++)
                strTemp += string.Format("{0:X2} ", data[i]);

        return strTemp;
    }
}
