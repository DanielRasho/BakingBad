using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ButtonSfx : MonoBehaviour
{
    [SerializeField] private SfxId sfxType = SfxId.UiClick;

    private Button _button;

    public static void Attach(Button button, SfxId sfxId)
    {
        if (button == null)
        {
            return;
        }

        ButtonSfx sfx = button.GetComponent<ButtonSfx>();
        if (sfx == null)
        {
            sfx = button.gameObject.AddComponent<ButtonSfx>();
        }

        sfx.sfxType = sfxId;
        sfx.Bind();
    }

    private void Awake()
    {
        Bind();
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(PlaySound);
        }
    }

    private void Bind()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        _button.onClick.RemoveListener(PlaySound);
        _button.onClick.AddListener(PlaySound);
    }

    private void PlaySound()
    {
        AudioManager.Play(sfxType);
    }
}
