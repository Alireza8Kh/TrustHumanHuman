using UnityEngine;

public class WaveMover : MonoBehaviour
{
    public float speed = 100f;

    void Update()
    {
        transform.Translate(Vector3.down * speed * Time.deltaTime);

        if (transform.position.y < -500f)
        {
            Destroy(gameObject); // cleanup after it's off screen
        }
    }
}
