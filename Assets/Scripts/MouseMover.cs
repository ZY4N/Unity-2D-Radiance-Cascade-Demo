using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseMover : MonoBehaviour {
    public Camera camera;

    void Update() {
        var mouse2D = (Vector2)camera.ScreenToWorldPoint(Input.mousePosition);
        transform.position = new Vector3(mouse2D.x, mouse2D.y, transform.position.z);
    }
}
