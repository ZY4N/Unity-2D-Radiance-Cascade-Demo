using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_Oscillator : MonoBehaviour {

    public Vector2 amplitude;
    public float frequency = 1.0f;

    private Vector2 m_center;

    // Start is called before the first frame update
    void Start() {
        m_center = transform.position;
    }

    // Update is called once per frame
    void Update() {
        var angle = Time.time * frequency * 2.0f * Mathf.PI;
        var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * amplitude;
        transform.position = m_center + offset;
    }
}
