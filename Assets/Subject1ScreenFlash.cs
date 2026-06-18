using System.Collections;
using UnityEngine;
using UnityEngine.UI;


public class Subject1ScreenFlash : MonoBehaviour
{
    public Image Subject1flashImage;
    public float flashDuration = 2.0f;

    void Start()
    {
        if (Subject1flashImage != null)
        {
            Color c = Subject1flashImage.color;
            c.a = 0f;
            Subject1flashImage.color = c;
        }
    }

    public void Subject1Flash()
    {
        if (gameObject.activeInHierarchy)
            StartCoroutine(FlashCoroutine());
    }

    IEnumerator FlashCoroutine()
    {
        if (Subject1flashImage == null) yield break;

        Color c = Subject1flashImage.color;
        c.a = 0.6f;
        Subject1flashImage.color = c;

        yield return new WaitForSeconds(flashDuration);

        c.a = 0f;
        Subject1flashImage.color = c;
    }
}

