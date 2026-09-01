using UnityEngine;

namespace UpDraftArt.EditorTools
{
    public class ButtonClick : MonoBehaviour
    {

        public string url = "https://assetstore.unity.com/packages/slug/358728";

        public void OpenLink()
        {
            Application.OpenURL(url);
        }
    }
}