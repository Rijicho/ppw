using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;

namespace RUtil.Editor
{
    public class CustomInspector : UnityEditor.Editor
    {
        public SerializedProperty GetP(string name) => serializedObject.FindProperty(name);
        protected void FindAllProperties<TVal>(Dictionary<TVal, SerializedProperty> dest)
            where TVal : Enum
        {
            serializedObject.FindAllProperties(dest);
        }
        protected static void DisplayAll<TVal>(ref bool foldout, string header, Dictionary<TVal, SerializedProperty> sps, Func<TVal, bool> altDrawer = default)
            where TVal : Enum
        {
            CustomInspectorUtil.BV("box");
            {
                foldout = CustomInspectorUtil.FoldoutShuriken(header, foldout);
                if (foldout)
                {
                    foreach (TVal v in Enum.GetValues(typeof(TVal)))
                    {
                        if (!altDrawer?.Invoke(v) ?? true)
                        {
                            if (sps[v].isArray && sps[v].propertyType != SerializedPropertyType.String)
                            {
                                CustomInspectorUtil.DisplayList(sps[v], sps[v].name, true);
                            }
                            else
                            {
                                EditorGUILayout.PropertyField(sps[v], true);
                            }
                        }
                    }
                }
            }
            CustomInspectorUtil.EV();
        }

        protected static void DisplayAll<TVal>(string header, Dictionary<TVal, SerializedProperty> sps, Func<TVal, bool> altDrawer = default)
            where TVal : Enum
        {
            using (new HeaderScope(header))
            {
                foreach (TVal v in Enum.GetValues(typeof(TVal)))
                {
                    if (!altDrawer?.Invoke(v) ?? true)
                    {
                        if (sps[v].isArray && sps[v].propertyType != SerializedPropertyType.String)
                        {
                            CustomInspectorUtil.DisplayList(sps[v], sps[v].name, true);
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(sps[v], true);
                        }
                    }
                }
            }
        }
    }

    public class CustomInspector<T> : CustomInspector
        where T : Enum
    {
        protected Dictionary<T, SerializedProperty> V = new Dictionary<T, SerializedProperty>();

        protected bool PF(T p, string label = "", params GUILayoutOption[] options)
        {
            if (string.IsNullOrEmpty(label))
            {
                return EditorGUILayout.PropertyField(V[p], true, options);
            }
            return EditorGUILayout.PropertyField(V[p], new GUIContent(label), true, options);
        }

        protected void PFArray(T p, string label = "", bool allowEmpty = true)
        {
            CustomInspectorUtil.DisplayList(V[p], label, allowEmpty);
        }

        protected virtual void OnEnable()
        {
            FindAllProperties(V);
        }

        public override void OnInspectorGUI()
        {
            DisplayAll(GetType().Name, V);
        }
    }

}
