using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Translation.Unity.Editor
{
    /// <summary> [SearchableEnum] enum 필드를 검색창 있는 드롭다운으로 그린다 </summary>
    [CustomPropertyDrawer(typeof(SearchableEnumAttribute))]
    public sealed class SearchableEnumDrawer : PropertyDrawer
    {
        private static readonly AdvancedDropdownState State = new AdvancedDropdownState();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            var rect = EditorGUI.PrefixLabel(position, label);

            var names = property.enumNames;
            var index = property.enumValueIndex;
            var current = index >= 0 && index < names.Length
                ? names[index]
                : "(없는 값: " + property.intValue + ")";

            if (property.hasMultipleDifferentValues)
                current = "—";

            if (EditorGUI.DropdownButton(rect, new GUIContent(current), FocusType.Keyboard))
            {
                var serializedObject = property.serializedObject;
                var path = property.propertyPath;
                var dropdown = new EnumDropdown(State, names, label.text, picked =>
                {
                    serializedObject.Update();
                    var target = serializedObject.FindProperty(path);
                    if (target == null)
                        return;

                    target.enumValueIndex = picked;
                    serializedObject.ApplyModifiedProperties();
                });
                dropdown.Show(rect);
            }

            EditorGUI.EndProperty();
        }

        private sealed class EnumDropdown : AdvancedDropdown
        {
            private readonly string[] _names;
            private readonly string _title;
            private readonly Action<int> _onPicked;

            public EnumDropdown(AdvancedDropdownState state, string[] names, string title, Action<int> onPicked)
                : base(state)
            {
                _names = names;
                _title = title;
                _onPicked = onPicked;
                minimumSize = new Vector2(280, 320);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(string.IsNullOrEmpty(_title) ? "키" : _title);
                for (var i = 0; i < _names.Length; i++)
                    root.AddChild(new AdvancedDropdownItem(_names[i]) { id = i });

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                _onPicked(item.id);
            }
        }
    }
}
