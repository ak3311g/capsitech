
using TMPro;
using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    [Header("Visual Component")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI healthText;

    public PlayerData LinkedData { get; private set;}
    public void Bind(PlayerData data)
    {
        LinkedData = data;
        UpdateVisual();
    }

    public void UpdateVisual()
    {
        if(LinkedData == null) return;

        if(spriteRenderer != null)
            spriteRenderer.color = LinkedData.PlayerColor;

        if(nameText != null)
            nameText.text = LinkedData.PlayerName;

        if(healthText != null)
            healthText.text = $"❤️{LinkedData.health}";
    }
}
