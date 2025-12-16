using Minigames;
using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    [HideInInspector] public int Number;                    // The logical number (1-8)
    [HideInInspector] public LezginkaSortGame Manager;

    [SerializeField] private Image backgroundImage;         // optional: for highlight

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(OnClick);
        }

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
    }

    public void Init(int number, Sprite sprite, LezginkaSortGame manager)
    {
        Number = number;
        Manager = manager;

        var img = GetComponent<Image>();
        img.sprite = sprite;
    }

    private void OnClick()
    {
        Manager.OnCardClicked(this);
    }

    public void SetSelected(bool selected)
    {
        if (!backgroundImage) return;

        backgroundImage.color = selected ? new Color(1f, 1f, 0.5f) : Color.white;
    }
}
