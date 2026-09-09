// Translation export 가 처음 한 번 만든 파일. 이 파일은 다시 쓰지 않으니 프로젝트에 맞게 고쳐도 된다.
using TMPro;
using Translation.Unity;
using UnityEngine;

namespace TranslationSample
{
    /// <summary>
    /// TMP 텍스트에 붙여 Key 를 고르면 지금 언어의 글자를 넣는다. 언어가 바뀌면 다시 넣는다.
    /// Key 가 None 이면 아무것도 하지 않아 손으로 적은 글자가 남는다
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Translation/Localize Text")]
    public class LocalizeText : MonoBehaviour
    {
        [SearchableEnum] public LocalKey Key;

        private TMP_Text _text;
        private object[] _args;

        public TMP_Text Text
        {
            get
            {
                if (_text == null)
                    _text = GetComponent<TMP_Text>();

                return _text;
            }
        }

        private void OnEnable()
        {
            TranslationRuntime.OnLanguageChanged += OnLanguageChanged;
            Refresh();
        }

        private void OnDisable()
        {
            TranslationRuntime.OnLanguageChanged -= OnLanguageChanged;
        }

        /// <summary> 키를 바꾸고 바로 넣는다. args 는 {0} {1} 자리에 들어간다 </summary>
        public void SetKey(LocalKey key, params object[] args)
        {
            Key = key;
            _args = args;
            Refresh();
        }

        /// <summary> {0} {1} 자리 값만 바꾼다 </summary>
        public void SetArgs(params object[] args)
        {
            _args = args;
            Refresh();
        }

        public void Refresh()
        {
            if (Key == LocalKey.None || Text == null)
                return;

            Text.text = _args == null || _args.Length == 0
                ? Localization.GetString(Key)
                : Localization.GetString(Key, _args);
        }

        private void OnLanguageChanged(string languageId)
        {
            Refresh();
        }
    }
}
