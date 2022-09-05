using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RUtil.Curve;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CurveUtility
{
    public class YukselTest : MonoBehaviour
    {
        [SerializeField] List<Vector3> cps = new List<Vector3>();
        [SerializeField] List<float> ws = new List<float>();
        [SerializeField] List<float> phis = new List<float>();
        [SerializeField] List<float> psis = new List<float>();
        [SerializeField] int stepPerSegment = 20;
        [SerializeField] bool loop = false;
        [SerializeField, Range(0.01f,14f)] float epsilon = 3f;
        [SerializeField] BezierCurve.CalcQMethod solver;
        [SerializeField] PPWCurve.BlendingScheme scheme;
        [SerializeField] Color color = Color.green;
        [SerializeField] float theta = 180;
        [SerializeField] bool showCurvature = true;
        [SerializeField] bool showInterpolationFunc = true;

        Vector3[] plotBuffer = new Vector3[100];

        PPWCurve.CurveData data;

        void OnDrawGizmos()
        {
            if (cps.Count < 3)
                return;
            while (ws.Count > cps.Count) ws.RemoveAt(ws.Count - 1);
            while (ws.Count < cps.Count) ws.Add(1);
            while (phis.Count > cps.Count) phis.RemoveAt(phis.Count - 1);
            while (phis.Count < cps.Count) phis.Add(1);
            while (psis.Count > cps.Count) psis.RemoveAt(phis.Count - 1);
            while (psis.Count < cps.Count) psis.Add(0);

            PPWCurve.CalcQMethod = solver;
            PPWCurve.Scheme = scheme;
            RKCurves.NewtonEpsilon = 0.001f;

            if (data == null)
            {
                data = new PPWCurve.CurveData(100, stepPerSegment);
            }

            data.IsClosed = loop;
            data.Init(
                cps.Zip(ws, (v, w) => new ControlPoint(v, w)),
                phis, 
                psis,
                stepPerSegment);

            PPWCurve.CalcAll(data);


            //int plotCount = YukselCurve.Calc(cps.Zip(ws,(v,w)=>new ControlPoint(v,w)).ToList(), YukselCurve.ItpFuncRBezier, ref plotBuffer, stepPerSegment, loop);                        
            int plotCount = data.ValidPlotLength;

            if (showCurvature)
            {
                Gizmos.color = new Color(color.r, color.g, color.b, 0.15f);
                for (int i = 0; i < plotCount; i++)
                {
                    if (i % data.PlotStepPerSegment == 0)
                        continue;
                    var dt = 1f / plotCount;
                    Vector2 pp = data.Plots[(i + plotCount - 1) % plotCount];
                    Vector2 p = data.Plots[i];
                    Vector2 np = data.Plots[(i + 1) % plotCount];
                    var dx1 = (p.x - pp.x) / dt;
                    var dy1 = (p.y - pp.y) / dt;
                    var dx2 = (np.x - p.x) / dt;
                    var dy2 = (np.y - p.y) / dt;
                    var ddx = (dx2 - dx1) / dt;
                    var ddy = (dy2 - dy1) / dt;
                    var k = (dx1 * ddy - dy1 * ddx) / Mathf.Pow(dx1 * dx1 + dy1 * dy1, 1.5f);
                    Gizmos.DrawLine(p, p + new Vector2(dy1, -dx1).normalized * k * 0.1f);
                }
            }
            if (showInterpolationFunc)
            {
                Gizmos.color = new Color(1, 1, 1, 0.3f);
                for (int i = data.IsClosed ? 0 : 1; i < (data.IsClosed ? data.ValidCPCount : data.ValidCPCount - 1); i++)
                {
                    for (int j = 0; j < data.Polygons[i].Count - 1; j++)
                    {
                        Vector3 p = data.Polygons[i][j];
                        Vector3 np = data.Polygons[i][j + 1];
                        Gizmos.DrawLine(p, np);
                    }
                }
            }


            Gizmos.color = color;
            for (int i = 0; i < plotCount - 1; i++)
            {
                Vector3 p = data.Plots[i];
                Vector3 np = data.Plots[i + 1];
                Gizmos.DrawLine(p, np);
            }
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(YukselTest))]
        public class YukselTestEditor : Editor
        {
            SerializedProperty cps, ws, loop, epsilon, phis, psis;
            YukselTest tg;

            private void OnEnable()
            {
                tg = target as YukselTest;
                cps = serializedObject.FindProperty("cps");
                ws = serializedObject.FindProperty("ws");
                loop = serializedObject.FindProperty("loop");
                epsilon = serializedObject.FindProperty("epsilon");
                phis = serializedObject.FindProperty("phis");
                psis = serializedObject.FindProperty("psis");
            }

            private void OnSceneGUI()
            {
                serializedObject.Update();

                Vector3 c = default;
                for (int i = 0; i < cps.arraySize; i++)
                {
                    var elem = cps.GetArrayElementAtIndex(i);
                    var prev = elem.vector3Value;
                    Handles.color = new Color(1, 1, 0, 0.7f);
                    Handles.DrawSolidDisc(prev, Vector3.back, 0.02f);
                    Handles.color = new Color(0, 1, 1, 0.5f);
                    var next = Handles.Slider2D(prev, Vector3.back, Vector3.right, Vector3.up, 0.1f, Handles.CircleHandleCap, 0.5f);
                    if (prev != next)
                    {
                        Undo.RecordObject(target, "move");
                        elem.vector3Value = next;
                    }
                    c += elem.vector3Value;
                }
                c /= cps.arraySize;

                serializedObject.ApplyModifiedProperties();


                var labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = Color.white;
                serializedObject.Update();

                while(ws.arraySize < cps.arraySize)
                {
                    ws.arraySize++;
                    ws.GetArrayElementAtIndex(ws.arraySize - 1).floatValue = 1;
                }
                if (ws.arraySize > cps.arraySize)
                    ws.arraySize = cps.arraySize;

                while (phis.arraySize < cps.arraySize)
                {
                    phis.arraySize++;
                    phis.GetArrayElementAtIndex(phis.arraySize - 1).floatValue = 1;
                }
                if (phis.arraySize > cps.arraySize)
                    phis.arraySize = cps.arraySize;

                while (psis.arraySize < cps.arraySize)
                {
                    psis.arraySize++;
                    psis.GetArrayElementAtIndex(psis.arraySize - 1).floatValue = 0;
                }
                if (psis.arraySize > cps.arraySize)
                    psis.arraySize = cps.arraySize;

                for (int i = 0; i < ws.arraySize; i++)
                {
                    if (!loop.boolValue && (i == 0 || i == cps.arraySize - 1))
                        continue;
                    Handles.color = Color.white;
                    var pos = cps.GetArrayElementAtIndex(i);


                    EditorGUI.BeginChangeCheck();
                    var w = ws.GetArrayElementAtIndex(i);
                    var q = Quaternion.Euler(0, 0, w.floatValue * 180f);
                    var id = GUIUtility.GetControlID(FocusType.Passive) + 1;
                    var changed = Handles.ScaleSlider(w.floatValue, pos.vector3Value, Vector3.up, Quaternion.identity, 0.2f, 0.1f);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(target, "Change Weight");
                        w.floatValue = Mathf.Max(changed, 0.001f);
                    }
                    Handles.Label(pos.vector3Value + new Vector3(0.02f, 0.25f), $"w={w.floatValue}", labelStyle);
                }


                for (int i = 0; i < phis.arraySize; i++)
                {
                    if (!loop.boolValue && (i == 0 || i >= cps.arraySize - 2))
                        continue;
                    Handles.color = Color.white;
                    var pos1 = cps.GetArrayElementAtIndex(i);
                    var pos2 = cps.GetArrayElementAtIndex((i + 1) % cps.arraySize);
                    var pos = (pos1.vector3Value + pos2.vector3Value) / 2;

                    EditorGUI.BeginChangeCheck();
                    var phi = phis.GetArrayElementAtIndex(i);
                    var q = Quaternion.Euler(0, 0, phi.floatValue * 180f);
                    var id = GUIUtility.GetControlID(FocusType.Passive) + 1;
                    var changed = Handles.ScaleSlider(phi.floatValue, pos, Vector3.up, Quaternion.identity, 0.2f, 0.1f);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(target, "Change Weight");
                        phi.floatValue = Mathf.Clamp(changed, 0.001f, 10f);
                    }
                    Handles.Label(pos + new Vector3(0.02f, 0.25f), $"Φ={phi.floatValue}", labelStyle);
                }



                //Handles.Label(c, $"e={2 / (1+Mathf.Pow(2.718281828f, epsilon.floatValue))}", labelStyle);
                serializedObject.ApplyModifiedProperties();
            }
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                serializedObject.Update();

                if(GUILayout.Button("Circular arc"))
                {
                    int n = cps.arraySize = ws.arraySize = phis.arraySize = Mathf.Max(3, Mathf.CeilToInt(tg.theta / 90) + 1);
                    if (tg.theta % 360 == 0)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            cps.GetArrayElementAtIndex(i).vector3Value = Quaternion.Euler(0, 0, 360 / 5 * i) * Vector3.right;
                            ws.GetArrayElementAtIndex(i).floatValue = Mathf.Cos(Mathf.PI * 2 / 5);
                            phis.GetArrayElementAtIndex(i).floatValue = 1;
                            loop.boolValue = true;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < n; i++)
                        {
                            cps.GetArrayElementAtIndex(i).vector3Value = Quaternion.Euler(0, 0, tg.theta / (n - 1) * i) * Vector3.right;
                            ws.GetArrayElementAtIndex(i).floatValue = Mathf.Cos(tg.theta / (n - 1) * Mathf.Deg2Rad);
                            phis.GetArrayElementAtIndex(i).floatValue = 1;
                            loop.boolValue = false;
                        }
                    }
                }


                serializedObject.ApplyModifiedProperties();
            }
        }
#endif

    }
}