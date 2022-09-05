using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace RUtil.Editor
{
    public static partial class CustomInspectorUtil
    {
        public static void Label(string label, params GUILayoutOption[] options) => EditorGUILayout.LabelField(label, options);
        public static void Label(string label, int fontsize, Font f = null, params GUILayoutOption[] options)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = fontsize;
            if (f != null) style.font = f;
            EditorGUILayout.LabelField(label, style, options);
        }
        public static void Label(string label, int fontsize, Font f, Color fontcolor, params GUILayoutOption[] options)
        {
            GUIStyle style = new GUIStyle();
            if (fontsize > 0)
                style.fontSize = fontsize;
            if (f != null) style.font = f;
            style.normal.textColor = fontcolor;
            EditorGUILayout.LabelField(label, style, options);
        }
        public static void Label(string label, GUIStyle style, params GUILayoutOption[] options)
            => EditorGUILayout.LabelField(label, style, options);
        public static void InfoLabel(string text, Font font = null, int indent = 0)
        {
            InfoLabel(text, Color.black, 12, font, indent);
        }
        public static void InfoLabel(string text, Color fontColor, int fontSize = 12, Font font = null, int indent = 0)
        {
            var style = WrappedLabelStyle(fontColor, font, fontSize);

            if (indent > 0)
                PushIndent(indent);
            Label(text, style);
            if (indent > 0)
                BackIndent(indent);
        }


        public static bool Display(SerializedProperty p, params GUILayoutOption[] options)
                => EditorGUILayout.PropertyField(p, true, options);

        public static bool Display(SerializedProperty p, string label, params GUILayoutOption[] options)
            => EditorGUILayout.PropertyField(p, new GUIContent(label), true, options);



        public static void DisplayPrefab(SerializedProperty p, string label, params GUILayoutOption[] options)
        {
            var keep = p.objectReferenceValue;
            Display(p, label, options);
            if (p.objectReferenceValue != null && PrefabUtility.GetPrefabAssetType(p.objectReferenceValue) != PrefabAssetType.Regular)
            {
                p.objectReferenceValue = keep;
                EditorApplication.Beep();
                EditorUtility.DisplayDialog("無効な操作", "インスタンスではなくプレハブを登録してください。", "OK", "Да");
            }
        }

        public static void DisplayList(SerializedProperty sp, string label, bool allowempty,
            Action<SerializedProperty, int> displayAction = null,
            Action<SerializedProperty, int> initializeAction = null,
            Action<SerializedProperty, int> actionAfterAdd = null,
            Action<SerializedProperty, int> actionOnChange = null,
            Action<int> actionAfterDelete = null,
            bool drawline = true, bool editable = true, bool insertable = true)
        {
            if (sp.isArray)
            {
                using (var horizontalScope = new EditorGUILayout.HorizontalScope(GUIStyle.none))
                {
                    if (label != "")
                        Label(label);
                }
                if (!allowempty && sp.arraySize == 0)
                {
                    sp.InsertArrayElementAtIndex(0);
                    initializeAction?.Invoke(sp.GetArrayElementAtIndex(0), 0);
                    actionAfterAdd?.Invoke(sp.GetArrayElementAtIndex(0), 0);
                }
                int willremove = -1;
                int willinsert = -1;
                for (int j = 0; j < sp.arraySize; j++)
                {
                    var elem = sp.GetArrayElementAtIndex(j);


                    using (new EditorGUILayout.HorizontalScope(GUIStyle.none))
                    {
                        using (new EditorGUILayout.VerticalScope(GUIStyle.none))
                        {
                            EditorGUI.BeginChangeCheck();
                            if (displayAction == null)
                            {
                                float tmp = EditorGUIUtility.labelWidth;
                                EditorGUIUtility.labelWidth = 45.0f + EditorGUI.indentLevel * 15;
                                EditorGUILayout.PropertyField(elem, new GUIContent("│#" + j.ToString() + " >"), true);
                                EditorGUIUtility.labelWidth = tmp;
                            }
                            else
                            {
                                displayAction(elem, j);
                            }
                            if (EditorGUI.EndChangeCheck())
                            {
                                actionOnChange?.Invoke(elem, j);
                            }
                        }
                        if (editable)
                        {
                            if (sp.arraySize > (allowempty ? 0 : 1) && GUILayout.Button("×", GUILayout.Width(30)))
                            {
                                willremove = j;
                            }
                            if (insertable && GUILayout.Button("+", GUILayout.Width(30)))
                            {
                                willinsert = j;
                            }
                        }
                    }
                }
                if (editable)
                {
                    var rect = EditorGUILayout.GetControlRect(false, 20);
                    if (GUI.Button(new Rect(rect.x + EditorGUI.indentLevel * 15, rect.y, 50, rect.height), "+"))
                    {
                        sp.InsertArrayElementAtIndex(sp.arraySize);
                        var elem = sp.GetArrayElementAtIndex(sp.arraySize - 1);
                        initializeAction?.Invoke(elem, sp.arraySize - 1);
                        actionAfterAdd?.Invoke(elem, sp.arraySize - 1);
                    }
                }
                if (editable && willremove != -1)
                {
                    sp.DeleteArrayElementAtIndex(willremove);
                    actionAfterDelete?.Invoke(willremove);
                }
                if (editable && insertable && willinsert != -1)
                {
                    sp.InsertArrayElementAtIndex(willinsert);
                    var elem = sp.GetArrayElementAtIndex(willinsert);
                    initializeAction?.Invoke(elem, willinsert);
                    actionAfterAdd?.Invoke(elem, willinsert);
                }

                if (sp.arraySize > 0 && drawline)
                {
                    Label("└───────────────────────────────────────────────────────────────");
                }
            }
            else
            {
                Label("Error: " + sp.name + "はリストではありません。");
            }
        }

        public static void DisplayPrefabList(SerializedProperty sp, string label, bool allowempty, bool drawline = true, bool editable = true, bool insertable = true)
        {
            DisplayList(sp, label, allowempty, (x, i) =>
            {
                float tmp = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 45.0f + EditorGUI.indentLevel * 15;
                DisplayPrefab(x, "│#" + i.ToString() + " >", GUILayout.MinWidth(200));
                EditorGUIUtility.labelWidth = tmp;
            }, null, null, null, null, drawline, editable, insertable);
        }


        public static void MakeLine() => EditorGUILayout.LabelField("───────────────────────────────────────────────────────────────");


        public static Rect BV(GUIStyle style = default, params GUILayoutOption[] options) => EditorGUILayout.BeginVertical(style ?? GUIStyle.none, options);
        public static void EV() => EditorGUILayout.EndVertical();
        public static Rect BH(GUIStyle style = default, params GUILayoutOption[] options) => EditorGUILayout.BeginHorizontal(style ?? GUIStyle.none, options);
        public static void EH() => EditorGUILayout.EndHorizontal();
        public static void BS(ref Vector2 scrollPos, GUIStyle style = default, params GUILayoutOption[] options) => scrollPos = EditorGUILayout.BeginScrollView(scrollPos, style ?? GUIStyle.none, options);
        public static void ES() => EditorGUILayout.EndScrollView();

        public static void fsp() => GUILayout.FlexibleSpace();

        public static void AutoRenameGameObject(UnityEngine.Object target, string name)
        {
            if (!PrefabUtility.IsPartOfPrefabAsset(target))
            {
                var prevname = target.name;
                target.name = name;
                if (string.IsNullOrEmpty(target.name))
                {
                    target.name = "<Set ID on Inspector>";
                }
                if (target.name != prevname)
                {
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }
        }

        public static void Button(string label, Action content, float sizex = -1, float sizey = -1, float maxsizex = -1, float maxsizey = -1)
        {
            List<GUILayoutOption> options = new List<GUILayoutOption>();
            if (sizex > 0) options.Add(GUILayout.Width(sizex));
            if (sizey > 0) options.Add(GUILayout.Height(sizey));
            if (maxsizex > 0) options.Add(GUILayout.MaxWidth(maxsizex));
            if (maxsizey > 0) options.Add(GUILayout.MaxHeight(maxsizey));

            if (GUILayout.Button(label, options.ToArray()))
            {
                content?.Invoke();
            }
        }

        public static bool Button(string label, float sizex = -1, float sizey = -1, float maxsizex = -1, float maxsizey = -1)
        {
            List<GUILayoutOption> options = new List<GUILayoutOption>();
            if (sizex > 0) options.Add(GUILayout.Width(sizex));
            if (sizey > 0) options.Add(GUILayout.Height(sizey));
            if (maxsizex > 0) options.Add(GUILayout.MaxWidth(maxsizex));
            if (maxsizey > 0) options.Add(GUILayout.MaxHeight(maxsizey));
            return GUILayout.Button(label, options.ToArray());
        }

        public static bool Button(string label, Color fontColor, Color backGroundColor, Font font = null, int fontSize = 0, float sizex = -1, float sizey = -1, float maxsizex = -1, float maxsizey = -1)
        {
            List<GUILayoutOption> options = new List<GUILayoutOption>();
            if (sizex > 0) options.Add(GUILayout.Width(sizex));
            if (sizey > 0) options.Add(GUILayout.Height(sizey));
            if (maxsizex > 0) options.Add(GUILayout.MaxWidth(maxsizex));
            if (maxsizey > 0) options.Add(GUILayout.MaxHeight(maxsizey));

            var tmpf = GUI.skin.button.font;
            var tmpfs = GUI.skin.button.fontSize;
            if (font != null)
            {
                GUI.skin.button.font = font;
            }
            GUI.skin.button.fontSize = fontSize;
            bool ret;
            using (new BackgroundColorScope(backGroundColor, fontColor))
            {
                ret = GUILayout.Button(label, GUI.skin.button, options.ToArray());
            }
            GUI.skin.button.font = tmpf;
            GUI.skin.button.fontSize = tmpfs;
            return ret;
        }


        public static bool Button(Rect rect, string label, Color fontColor, Color backGroundColor, Font font = null, int fontSize = 0)
        {
            var tmpf = GUI.skin.button.font;
            var tmpfs = GUI.skin.button.fontSize;
            if (font != null)
            {
                GUI.skin.button.font = font;
            }
            GUI.skin.button.fontSize = fontSize;
            bool ret;
            using (new BackgroundColorScope(backGroundColor, fontColor))
            {
                ret = GUI.Button(rect, label, GUI.skin.button);
            }
            GUI.skin.button.font = tmpf;
            GUI.skin.button.fontSize = tmpfs;
            return ret;
        }

        public static bool ToggleButton(Rect rect, bool isOn, string label, Color fontColor, Color backGroundColor, Font font = null, int fontSize = 0)
        {
            using (new BackgroundColorScope(backGroundColor))
            {
                var tmpf = GUI.skin.button.font;
                var tmpfs = GUI.skin.button.fontSize;
                var tmp = GUI.skin.button.normal.textColor;
                var tmp2 = GUI.skin.button.onNormal.textColor;
                if (font != null)
                {
                    GUI.skin.button.font = font;
                }
                GUI.skin.button.fontSize = fontSize;
                GUI.skin.button.normal.textColor = fontColor;
                GUI.skin.button.onNormal.textColor = fontColor;
                var ret = GUI.Toggle(rect, isOn, label, GUI.skin.button);
                GUI.skin.button.normal.textColor = tmp;
                GUI.skin.button.onNormal.textColor = tmp2;
                GUI.skin.button.font = tmpf;
                GUI.skin.button.fontSize = tmpfs;
                return ret;
            }
        }

        public static bool ToggleButton(bool isOn, string label, float sizex = -1, float sizey = -1, float maxsizex = -1, float maxsizey = -1)
        {
            List<GUILayoutOption> options = new List<GUILayoutOption>();
            if (sizex > 0) options.Add(GUILayout.Width(sizex));
            if (sizey > 0) options.Add(GUILayout.Height(sizey));
            if (maxsizex > 0) options.Add(GUILayout.MaxWidth(maxsizex));
            if (maxsizey > 0) options.Add(GUILayout.MaxHeight(maxsizey));

            return GUILayout.Toggle(isOn, label, "button", options.ToArray());
        }

        public static bool ToggleButton(bool isOn, string label, GUIStyle style, float sizex = -1, float sizey = -1, float maxsizex = -1, float maxsizey = -1)
        {
            List<GUILayoutOption> options = new List<GUILayoutOption>();
            if (sizex > 0) options.Add(GUILayout.Width(sizex));
            if (sizey > 0) options.Add(GUILayout.Height(sizey));
            if (maxsizex > 0) options.Add(GUILayout.MaxWidth(maxsizex));
            if (maxsizey > 0) options.Add(GUILayout.MaxHeight(maxsizey));

            var hover = style.onHover.background;
            style.onHover.background = isOn ? style.active.background : style.normal.background;
            var ret = GUILayout.Toggle(isOn, label, style, options.ToArray());
            style.onHover.background = hover;
            return ret;
        }


        public static bool ToggleButton(bool isOn, string label, Color fontColor, Color bgColor, float sizex = -1, float sizey = -1, float maxsizex = -1, float maxsizey = -1, Font font = null, int fontSize = 0)
        {
            List<GUILayoutOption> options = new List<GUILayoutOption>();
            if (sizex > 0) options.Add(GUILayout.Width(sizex));
            if (sizey > 0) options.Add(GUILayout.Height(sizey));
            if (maxsizex > 0) options.Add(GUILayout.MaxWidth(maxsizex));
            if (maxsizey > 0) options.Add(GUILayout.MaxHeight(maxsizey));

            bool ret = isOn;
            using (new BackgroundColorScope(bgColor))
            {
                var tmp = GUI.skin.button.normal.textColor;
                var tmp2 = GUI.skin.button.onNormal.textColor;
                var tmpf = GUI.skin.button.font;
                var tmpfs = GUI.skin.button.fontSize;
                GUI.skin.button.normal.textColor = fontColor;
                GUI.skin.button.onNormal.textColor = fontColor;
                if (font != null)
                {
                    GUI.skin.button.font = font;
                }
                GUI.skin.button.fontSize = fontSize;
                ret = GUILayout.Toggle(ret, label, GUI.skin.button, options.ToArray());
                GUI.skin.button.normal.textColor = tmp;
                GUI.skin.button.onNormal.textColor = tmp2;
                GUI.skin.button.font = tmpf;
                GUI.skin.button.fontSize = tmpfs;
            }

            return ret;
        }


        public static bool FoldoutShuriken(string title, bool display)
        {
            var style = new GUIStyle("ShurikenModuleTitle");
            style.font = new GUIStyle(EditorStyles.label).font;
            style.border = new RectOffset(15, 7, 4, 4);
            style.fixedHeight = 22;
            style.contentOffset = new Vector2(20f, -2f);
            style.stretchWidth = true;

            var rect = GUILayoutUtility.GetRect(16f, 22f, style, GUILayout.ExpandWidth(true));
            GUI.Box(rect, title, style);

            var e = Event.current;

            var toggleRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
            if (e.type == EventType.Repaint)
            {
                EditorStyles.foldout.Draw(toggleRect, false, false, display, false);
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                display = !display;
                e.Use();
            }

            return display;
        }

        public static void ToolBar(ref int selected, params string[] texts)
        {
            ToolBar(ref selected, null, texts);
        }

        public static void ToolBar(ref int selected, Font font, params string[] texts)
        {
            if (texts.Length == 0)
                return;
            selected = Mathf.Clamp(selected, 0, texts.Length - 1);

            var style = new GUIStyle(EditorStyles.toolbarButton);
            style.fontSize = 0;
            style.fixedHeight = 25;
            if (font != null)
                style.font = font;
            selected = GUILayout.Toolbar(selected, texts, style);
        }


        public static void ToolBar(ref int selected, Font font, params (string title, Action content)[] contents)
        {
            if (contents.Length == 0)
                return;
            selected = Mathf.Clamp(selected, 0, contents.Length - 1);

            var style = new GUIStyle(EditorStyles.toolbarButton);
            style.fontSize = 0;
            style.fixedHeight = 25;
            if (font != null)
                style.font = font;
            selected = GUILayout.Toolbar(selected, contents.Select(c => c.title).ToArray(), style);

            contents[selected].content?.Invoke();
        }

        public static void PushIndent(int i = 1) => EditorGUI.indentLevel += i;
        public static void BackIndent(int i = 1) => EditorGUI.indentLevel -= i;


        /// <summary>
        /// 端で折り返すラベル用GUIStyle
        /// </summary>
        /// <param name="fontcolor"></param>
        /// <returns></returns>
        public static GUIStyle WrappedLabelStyle(Color fontcolor, Font font = default, int fontsize = 0)
        {
            var ret = new GUIStyle(GUI.skin.label) { richText = true };
            ret.wordWrap = true;
            ret.normal.textColor = fontcolor;
            if (font != null)
                ret.font = font;
            if (fontsize > 0)
                ret.fontSize = fontsize;
            return ret;
        }

        public static void EnumPopup<TEnum>(SerializedProperty sp, params GUILayoutOption[] options) where TEnum : Enum
        {
            sp.intValue = CastTo<int>.From((TEnum)EditorGUILayout.EnumPopup(CastTo<TEnum>.From(sp.intValue), options));
        }

        public static void EnumPopup<TEnum>(string label, SerializedProperty sp, params GUILayoutOption[] options) where TEnum : Enum
        {
            sp.intValue = CastTo<int>.From((TEnum)EditorGUILayout.EnumPopup(label, CastTo<TEnum>.From(sp.intValue), options));
        }

        public static void EnumFlagField<TEnum>(SerializedProperty sp, params GUILayoutOption[] options) where TEnum : Enum
        {
            sp.intValue = CastTo<int>.From((TEnum)EditorGUILayout.EnumFlagsField(CastTo<TEnum>.From(sp.intValue), options));
        }

        public static void EnumFlagField<TEnum>(string label, SerializedProperty sp, params GUILayoutOption[] options) where TEnum : Enum
        {
            sp.intValue = CastTo<int>.From((TEnum)EditorGUILayout.EnumFlagsField(label, CastTo<TEnum>.From(sp.intValue), options));
        }

        public static void MakeHeader(string text, Font font = default, int fontsize = 15)
        {
            Label(text, fontsize, font);
            GUILayout.Box(GUIContent.none, GUILayout.Height(1), GUILayout.Width(200));
        }


        public static int RadioButtonVertical(Rect rect, int selected, IReadOnlyList<string> contents, Font font = null, int fontSize = 0)
        {
            var buttonHeight = rect.height / contents.Count;

            for (int i = 0; i < contents.Count; i++)
            {
                var content = contents[i];
                var buttonRect = new Rect { size = new Vector2(rect.width, buttonHeight), position = rect.min + Vector2.up * i * buttonHeight };
                bool prev = selected == i;
                bool now = ToggleButton(buttonRect, prev, content, Color.black, Color.white, font, fontSize);
                if (prev != now)
                {
                    return now ? i : -1;
                }
            }
            return selected;
        }
        public static int RadioButtonVertical(float width, float height, int selected, IReadOnlyList<string> contents, Color bgColor, Color fontColor, Font font = null, int fontSize = 0)
        {
            var buttonHeight = height / contents.Count;


            for (int i = 0; i < contents.Count; i++)
            {
                var content = contents[i];
                bool prev = selected == i;
                bool now = ToggleButton(prev, content, prev ? Color.white : fontColor, bgColor, width, buttonHeight, -1, -1, font, fontSize);
                if (prev != now)
                {
                    return now ? i : -1;
                }
            }
            return selected;
        }
        public static int RadioButtonVertical(int selected, IReadOnlyList<string> contents, Color bgColor, Color fontColor, Font font = null, int fontSize = 0)
        {
            return RadioButtonVertical(-1, -1, selected, contents, bgColor, fontColor, font, fontSize);
        }


        public static void FilterField(Rect rect, ref string filter, ref bool filterUnfocusFlag, string filterControlName = "__FilterField__")
        {
            var e = Event.current;

            if (GUI.GetNameOfFocusedControl() == filterControlName)
            {
                if (filterUnfocusFlag || e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
                {
                    EditorGUI.FocusTextInControl("");
                }
            }

            filterUnfocusFlag = false;

            float deleteWidth = 25;

            GUI.SetNextControlName(filterControlName);
            filter = GUI.TextField(new Rect(rect) { width = rect.width - deleteWidth }, filter, "SearchTextField");

            if (e.type == EventType.MouseDown && e.button == 0 && !rect.Contains(e.mousePosition))
            {
                filterUnfocusFlag = true;
            }

            if (GUI.Button(new Rect(rect.xMax - deleteWidth, rect.y, deleteWidth, rect.height), "Clear", "SearchCancelButton"))
            {
                filter = "";
            }
        }

        public static void FilterField(ref string filter, ref bool filterUnfocusFlag, string filterControlName = "__FilterField__")
        {
            var e = Event.current;
            BH();
            {
                if (GUI.GetNameOfFocusedControl() == filterControlName)
                {
                    if (filterUnfocusFlag || e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
                    {
                        EditorGUI.FocusTextInControl("");
                    }
                }

                filterUnfocusFlag = false;

                GUI.SetNextControlName(filterControlName);
                filter = GUILayout.TextField(filter, "SearchTextField");
                var lastrect = GUILayoutUtility.GetLastRect();

                if (e.type == EventType.MouseDown && e.button == 0 && !lastrect.Contains(e.mousePosition))
                {
                    filterUnfocusFlag = true;
                }

                GUI.enabled = !string.IsNullOrEmpty(filter);
                if (GUILayout.Button("Clear", "SearchCancelButton"))
                {
                    filter = string.Empty;
                }
                GUI.enabled = true;


            }
            EH();
        }


        public static string SerializedPropertyToString(SerializedProperty sp)
        {
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return sp.intValue.ToString();
                case SerializedPropertyType.Float:
                    return sp.floatValue.ToString();
                case SerializedPropertyType.String:
                    return sp.stringValue;
                case SerializedPropertyType.Boolean:
                    return sp.boolValue.ToString();
                case SerializedPropertyType.Enum:
                    if (sp.enumValueIndex < 0)
                    {
                        var i = sp.intValue;
                        var ret = "";
                        int j = 0;
                        bool first = true;
                        while (i > 0)
                        {
                            if (i % 2 == 1)
                            {
                                ret += (first ? "" : ", ") + sp.enumNames[j];
                                first = false;
                            }
                            i /= 2;
                            j++;
                        }
                        return ret;
                    }
                    else
                        return sp.enumNames[sp.enumValueIndex];
                case SerializedPropertyType.Vector3:
                    return sp.vector3Value.ToString();
                case SerializedPropertyType.Vector2:
                    return sp.vector2Value.ToString();
                case SerializedPropertyType.Quaternion:
                    return sp.quaternionValue.ToString();
                case SerializedPropertyType.Color:
                    return sp.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return sp.objectReferenceValue.ToString();
                default:
                    return "<Instance of " + sp.type + ">";
            }
        }
    }


    public static partial class SerializedObjectExtensions
    {
        public static void FindAllProperties<TVal>(this SerializedObject obj, Dictionary<TVal, SerializedProperty> dest, string separator = "__")
        {
            foreach (TVal v in Enum.GetValues(typeof(TVal)))
            {
                dest[v] = obj.FindProperty(v.ToString().Replace(separator, "."));
            }
        }
    }

    public static class CastTo<T>
    {
        private static class Cache<S>
        {
            static Cache()
            {
                var p = Expression.Parameter(typeof(S));
                var c = Expression.ConvertChecked(p, typeof(T));
                Caster = Expression.Lambda<Func<S, T>>(c, p).Compile();
            }
            internal static readonly Func<S, T> Caster;
        }

        public static T From<S>(S source)
        {
            return Cache<S>.Caster(source);
        }
    }

    public class BackgroundColorScope : GUI.Scope
    {
        private readonly Color color;
        private readonly Color fontColor;
        public BackgroundColorScope(Color color)
        {
            this.color = GUI.backgroundColor;
            this.fontColor = Color.black;
            GUI.backgroundColor = color;
        }

        public BackgroundColorScope(Color color, Color fontColor)
        {
            this.color = GUI.backgroundColor;
            this.fontColor = GUI.skin.label.normal.textColor;
            GUI.backgroundColor = color;
            GUI.skin.label.normal.textColor = fontColor;
            GUI.skin.button.normal.textColor = fontColor;
        }

        protected override void CloseScope()
        {
            GUI.backgroundColor = color;
            GUI.skin.label.normal.textColor = fontColor;
            GUI.skin.button.normal.textColor = fontColor;
        }
    }

    public class FontColorScope : GUI.Scope
    {
        private readonly Color color;
        private readonly Color buttonColor;
        public FontColorScope(Color color)
        {
            this.color = GUI.skin.label.normal.textColor;
            this.buttonColor = GUI.skin.button.normal.textColor;
            GUI.skin.label.normal.textColor = color;
            GUI.skin.button.normal.textColor = color;
        }


        protected override void CloseScope()
        {
            GUI.skin.label.normal.textColor = color;
            GUI.skin.button.normal.textColor = buttonColor;
        }

    }

    public class HeaderScope : GUI.Scope
    {
        public HeaderScope(string header, GUIStyle style = default, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(style ?? GUIStyle.none, options);
            CustomInspectorUtil.MakeHeader(header);
        }
        public HeaderScope(string header, Font headerFont, GUIStyle style = default, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(style ?? GUIStyle.none, options);
            CustomInspectorUtil.MakeHeader(header, headerFont);
        }
        public HeaderScope(string header, Font headerFont, int headerFontSize, GUIStyle style = default, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(style ?? GUIStyle.none, options);
            CustomInspectorUtil.MakeHeader(header, headerFont, headerFontSize);
        }

        protected override void CloseScope()
        {
            EditorGUILayout.EndVertical();
        }
    }
}






