using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_ColorChanger : MonoBehaviour {

    public Color[] colors = new[]{
        Color.blue, Color.cyan, Color.green, Color.yellow, Color.red, Color.magenta
    };
    public float colorsPerSecond = 0.5f;

    private Image m_image;

    void Start() {
        m_image = GetComponent<Image>();
    }

    void Update() {
        var t = Time.time * colorsPerSecond;
        var colorIndexFrom = (int)t % colors.Length;
        var colorIndexTo = (colorIndexFrom + 1) % colors.Length;
        var color = Color.Lerp(
            colors[colorIndexFrom],
            colors[colorIndexTo],
            t - colorIndexFrom
        );
        m_image.color = color;
    }
}
