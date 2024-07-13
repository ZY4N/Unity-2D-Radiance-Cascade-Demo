using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_Rotator : MonoBehaviour {

    public float angularVelocity = 20.0f;

    void Update()  {
        transform.Rotate(0, 0, angularVelocity * Time.deltaTime); 
    }
}
