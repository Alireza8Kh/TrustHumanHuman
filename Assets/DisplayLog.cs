using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DisplayLog : MonoBehaviour
{
    private TextMeshProUGUI tmpText;
    private Queue<string> logQueue = new Queue<string>();
    private const int maxLogCount = 5;

    // Start is called before the first frame update
    void Start()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
        Application.logMessageReceived += HandleLog;

    }
    void HandleLog(string logText, string stackTrace, LogType logType)
    {
        // Add the log message to the queue
        if (!logQueue.Contains(logText))
        {
            logQueue.Enqueue(logText);
        }

        // If the queue size exceeds the maximum log count, remove the oldest message
        if (logQueue.Count > maxLogCount)
        {
            logQueue.Dequeue();
        }

        // Update the TMP text component with the last 5 log messages
        UpdateText();
    }

    // Method to update the TMP text component with the last 5 log messages
    void UpdateText()
    {
        // Construct the text to display the last 5 log messages
        string displayText = "";
        foreach (string logMessage in logQueue)
        {
            displayText += logMessage + "\n";
        }

        // Set the TMP text component's text to the constructed display text
        tmpText.text = displayText;
    }

    void OnDestroy()
    {
        // Unsubscribe from Unity's debug log event to prevent memory leaks
        Application.logMessageReceived -= HandleLog;
    }
}
