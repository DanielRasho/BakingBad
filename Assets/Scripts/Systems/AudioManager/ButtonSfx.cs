using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSfx : MonoBehaviour
{
    public enum ButtonSfxType
    {
        Menu,
        Other
    }

    [SerializeField] private ButtonSfxType sfxType = ButtonSfxType.Menu;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(PlaySound);
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(PlaySound);
        }
    }

    private void PlaySound()
    {
        if (AudioManager.Instance == null) return;

        switch (sfxType)
        {
            case ButtonSfxType.Menu:
                AudioManager.Instance.PlayMenuClickSound();
                break;
            case ButtonSfxType.Other:
                AudioManager.Instance.PlayOtherClickSound();
                break;
        }
    }
}