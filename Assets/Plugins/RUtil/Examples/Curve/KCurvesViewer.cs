using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using static RUtil.Curve.KCurves;
#endif


public class KCurvesViewer : MonoBehaviour
{
    [SerializeField] LineRenderer rdr;
    [SerializeField] Vector3[] path = new Vector3[] { Vector3.zero, Vector3.one };
    [SerializeField] [HideInInspector] Vector3[] plot = default;
    [SerializeField] bool isLoop = true;
    [SerializeField] bool isShowUserControl = true;
    [SerializeField] bool isShowBezierControl = false;
    [SerializeField] int stepPerSegment = 20;
    [SerializeField] int loopCount = 10;

    public Vector3[] Path { get => path; set => path = value; }
    public Vector3[] Plot => plot;

    private void Update()
    {
        if (rdr)
        {
            rdr.positionCount = plot.Length;
            rdr.SetPositions(plot);
        }
    }

#if UNITY_EDITOR

    private void OnDrawGizmos()
    {
        if (plot != null && plot.Length >= 2)
        {
            for (int i = 0; i < plot.Length - 1; i++)
            {
                Handles.color = Color.green;// i % 2 == 0 ? Color.green : new Color(0, 0.5f, 0);
                Handles.DrawLine(plot[i] + transform.position, plot[(i + 1) % plot.Length] + transform.position);
            }
        }
    }

    [CustomEditor(typeof(KCurvesViewer))]
    public class KCurvesViewerEditor : Editor
    {
        SerializedProperty rdr;
        SerializedProperty path;
        SerializedProperty plot;
        SerializedProperty isLoop;
        SerializedProperty isShowUserControl;
        SerializedProperty isShowBezierControl;
        SerializedProperty stepPerSegment;
        SerializedProperty loopCount;

        KCurvesViewer tg;

        private void OnEnable()
        {
            tg = target as KCurvesViewer;
            rdr = serializedObject.FindProperty("rdr");
            path = serializedObject.FindProperty("path");
            plot = serializedObject.FindProperty("plot");
            isLoop = serializedObject.FindProperty("isLoop");
            isShowUserControl = serializedObject.FindProperty("isShowUserControl");
            isShowBezierControl = serializedObject.FindProperty("isShowBezierControl");
            stepPerSegment = serializedObject.FindProperty("stepPerSegment");
            loopCount = serializedObject.FindProperty("loopCount");
            Allocate();
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            Plot();
        }

        CalcSpace cSpace;
        PlotSpace pSpace;
        void Allocate()
        {
            cSpace = new CalcSpace(tg.path.Length);
            pSpace = new PlotSpace(path.arraySize, tg.stepPerSegment, tg.isLoop);
        }

        void Plot()
        {
            var controls = CalcBezierControls(tg.path, cSpace, tg.loopCount, tg.isLoop);

            var output = CalcPlots(controls, pSpace, tg.stepPerSegment, tg.isLoop);

            serializedObject.Update();
            plot.arraySize = output.Length;
            for (int i = 0; i < output.Length; i++)
            {
                plot.GetArrayElementAtIndex(i).vector3Value = output[i];
            }
            serializedObject.ApplyModifiedProperties();
        }


        bool isReCalcRequired;
        bool isReAllocRequired;
        bool prevIsLoop;
        int prevPathCount;
        int prevStepPerSegment;
        int prevLoopCount;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(rdr, new GUIContent("Renderer"));
            EditorGUILayout.PropertyField(isLoop, new GUIContent("Close Path"));
            EditorGUILayout.PropertyField(loopCount, new GUIContent("Iteration"));
            EditorGUILayout.PropertyField(stepPerSegment, new GUIContent("Density"));
            if (stepPerSegment.intValue <= 0)
                stepPerSegment.intValue = 1;
            EditorGUILayout.PropertyField(isShowUserControl, new GUIContent("Show Control Points"));
            EditorGUILayout.PropertyField(isShowBezierControl, new GUIContent("Show Bezier Outlines"));
            if (GUILayout.Button("Reset Position"))
            {
                for (int i = 0; i < tg.path.Length; i++)
                {
                    var theta = Mathf.PI * 2 / tg.path.Length * i;
                    path.GetArrayElementAtIndex(i).vector3Value = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta));
                }
                isReCalcRequired = true;
            }
            EditorGUILayout.PropertyField(path, new GUIContent("Path Data"), true);

            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("Recalc"))
            {
                isReCalcRequired = true;
            }
        }

        private void OnSceneGUI()
        {
            int movingHandle = -1;
            if (tg.isShowUserControl)
            {
                serializedObject.Update();
                for (int i = 0; i < path.arraySize; i++)
                {
                    Handles.color = Color.white;
                    var pos = path.GetArrayElementAtIndex(i);
                    EditorGUI.BeginChangeCheck();
                    var id = GUIUtility.GetControlID(i, FocusType.Passive);
                    var changed = Handles.FreeMoveHandle(id, pos.vector3Value + tg.transform.position, Quaternion.identity, 0.07f, new Vector3(0.5f, 0.5f, 0.5f), Handles.CircleHandleCap);
                    Handles.DrawSolidDisc(changed, Vector3.back, 0.01f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(target, "Move Path");
                        pos.vector3Value = changed - tg.transform.position;
                        isReCalcRequired = true;
                    }
                    if (GUIUtility.hotControl == id)
                    {
                        movingHandle = i;
                    }
                }
                serializedObject.ApplyModifiedProperties();
            }

            if (prevIsLoop != tg.isLoop)
            {
                isReAllocRequired = true;
                prevIsLoop = tg.isLoop;
            }
            if (prevPathCount != tg.path.Length)
            {
                isReAllocRequired = true;
                prevPathCount = tg.path.Length;
            }
            if (prevStepPerSegment != tg.stepPerSegment)
            {
                isReAllocRequired = true;
                prevStepPerSegment = tg.stepPerSegment;
            }
            if (prevLoopCount != tg.loopCount)
            {
                isReCalcRequired = true;
                prevLoopCount = tg.loopCount;
            }
            if (plot.arraySize == 0 && path.arraySize > 0)
            {
                isReAllocRequired = true;
            }

            if (isReAllocRequired)
            {
                Allocate();
                isReAllocRequired = false;
                isReCalcRequired = true;
            }

            if (isReCalcRequired)
            {
                Plot();
                isReCalcRequired = false;
            }

            if (tg.isShowBezierControl)
            {
                var cs = cSpace.Result;
                var len = cs.SegmentCount;
                for (int i = tg.isLoop ? 0 : 1; i < (tg.isLoop ? len : (len - 1)); i++)
                {
                    Handles.color = i % 3 == 0 ? Color.magenta : i % 3 == 1 ? Color.yellow : Color.cyan;
                    Handles.color -= new Color(0, 0, 0, 0.5f);
                    Handles.DrawLine(cs[i, 0] + tg.transform.position, cs[i, 1] + tg.transform.position);
                    Handles.DrawLine(cs[i, 1] + tg.transform.position, cs[i, 2] + tg.transform.position);
                    Handles.DrawWireCube(cs[i, 0] + tg.transform.position, Vector3.one * 0.02f);
                    Handles.DrawWireCube(cs[i, 1] + tg.transform.position, Vector3.one * 0.02f);
                    Handles.DrawWireCube(cs[i, 2] + tg.transform.position, Vector3.one * 0.02f);
                }
            }
        }

        static Vector3 BackCoord(Vector3 v, Vector3 P, Vector3 Q, Vector3 R)
        {
            v /= (v.x + v.y + v.z);
            return v.x * P + v.y * Q + v.z * R;
        }



        [MenuItem("Tools/KCurves/PerformanceTest3D")]
        static void _()
        {
            int n = 30;
            int iter = 10;
            int loop = 10000;

            var path = new bool[n].Select(_ => new Vector3(Random.value, Random.value)).ToArray();
            var cSpace = new CalcSpace(n);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < loop; i++)
            {
                CalcBezierControls(path, cSpace, iter, true);
            }
            Debug.Log((double)sw.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency / 10.0);
        }
    }


#endif

}