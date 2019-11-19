using TMPro;
using UnityEngine;

public class textOverlay : MonoBehaviour
{

    [SerializeField]
    private TextMeshPro OverlayText;

    private void Awake()
    {
        OverlayText = GetComponentInChildren<TextMeshPro>();
    }

    public void SetText(string text)
    {
        OverlayText.text = text;
    }

    public void SetFontSize(float size)
    {
        OverlayText.fontSize = size;
    }
}
