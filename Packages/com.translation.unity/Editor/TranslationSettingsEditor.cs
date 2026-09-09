using System;
using UnityEditor;
using UnityEngine;

namespace Translation.Unity.Editor
{
    /// <summary> 설정 에셋 인스펙터. 아래 버튼으로 바로 돌린다 </summary>
    [CustomEditor(typeof(TranslationSettings))]
    public sealed class TranslationSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var settings = (TranslationSettings)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("실행", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sync: 게임 DB 원문으로 번역 CSV 를 최신화한다\n"
                + "Validate: 번역 CSV 를 검사한다\n"
                + "Export: 번역 CSV 를 언어별 JSON 으로 굽는다\n"
                + "게임 DB 는 JSON · CSV, 시트는 CSV. 엑셀·구글 시트는 설정 파일을 뽑아 translation.exe 로 돌린다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync"))
                    Run("sync", () => TranslationEditorRunner.Sync(settings));

                if (GUILayout.Button("Sync (미리 보기)"))
                    Run("sync", () => TranslationEditorRunner.Sync(settings, dryRun: true));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate"))
                    Run("validate", () => TranslationEditorRunner.Validate(settings));

                if (GUILayout.Button("Export"))
                    Run("export", () => TranslationEditorRunner.Export(settings));
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("설정 파일 내보내기 (translation.config.json + bat)"))
                Run("설정 파일", () => TranslationEditorRunner.WriteConfigFiles(settings));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("경로", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("게임 DB", settings.GameDataDirectory ?? "(없음)");
                EditorGUILayout.TextField("번역 CSV", settings.SheetDirectory);
                EditorGUILayout.TextField("결과", settings.OutputDirectory);
                EditorGUILayout.TextField("생성 코드", settings.ScriptDirectory ?? "(안 만듦)");
            }
        }

        private static void Run(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError("[Translation] " + name + " 실패: " + e.Message);
                EditorUtility.DisplayDialog("Translation", name + " 실패\n\n" + e.Message, "확인");
            }
        }
    }
}
