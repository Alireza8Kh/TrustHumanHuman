using System.Collections;
using UnityEngine;
using UnityEngine.UI;


public class Subject2ScreenFlash : MonoBehaviour
{
    public Image Subject2flashImage;
    public float flashDuration = 0.2f;

    void Start()
    {
        if (Subject2flashImage != null)
        {
            Color c = Subject2flashImage.color;
            c.a = 0f;
            Subject2flashImage.color = c;
        }
    }

    public void Subject2Flash()
    {
        if (gameObject.activeInHierarchy)
            StartCoroutine(FlashCoroutine());
    }

    IEnumerator FlashCoroutine()
    {
        if (Subject2flashImage == null) yield break;

        Color c = Subject2flashImage.color;
        c.a = 0.6f;
        Subject2flashImage.color = c;

        yield return new WaitForSeconds(flashDuration);

        c.a = 0f;
        Subject2flashImage.color = c;
    }
}
