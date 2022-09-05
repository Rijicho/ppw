using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using static RUtil.Curve.KCurves2D;
#endif


public class KCurvesViewer2D : MonoBehaviour
{
    [SerializeField] Vector2[] path = new Vector2[] { Vector2.zero, Vector2.one };
    [SerializeField] [HideInInspector] Vector2[] plot = default;
    [SerializeField] bool isLoop = true;
    [SerializeField] bool isShowUserControl = true;
    [SerializeField] bool isShowBezierControl = false;
    [SerializeField] int stepPerSegment = 20;
    [SerializeField] int loopCount = 10;

    public Vector2[] Path { get => path; set => path = value; }
    public Vector2[] Plot => plot;

#if UNITY_EDITOR

    private void OnDrawGizmos()
    {
        if (plot != null && plot.Length >= 2)
        {
            for (int i = 0; i < plot.Length - 1; i++)
            {
                Handles.color = i % 2 == 0 ? Color.green : new Color(0, 0.5f, 0);
                Handles.DrawLine((Vector3)plot[i] + transform.position, (Vector3)plot[(i + 1) % plot.Length] + transform.position);
            }
        }
    }

    [CustomEditor(typeof(KCurvesViewer2D))]
    public class KCurvesViewer2DEditor : Editor
    {
        SerializedProperty path;
        SerializedProperty plot;
        SerializedProperty isLoop;
        SerializedProperty isShowUserControl;
        SerializedProperty isShowBezierControl;
        SerializedProperty stepPerSegment;
        SerializedProperty loopCount;

        KCurvesViewer2D tg;

        private void OnEnable()
        {
            tg = target as KCurvesViewer2D;
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

        CalcSpace cSpace;
        PlotSpace pSpace;
        void Allocate()
        {
            cSpace = new CalcSpace(tg.path.Length);
            pSpace = new PlotSpace(path.arraySize, tg.stepPerSegment, tg.isLoop);
        }

        void Plot()
        {
            var output = CalcPlots(tg.path, cSpace, pSpace, tg.loopCount, tg.stepPerSegment, tg.isLoop);

            serializedObject.Update();
            plot.arraySize = output.Length;
            for (int i = 0; i < output.Length; i++)
            {
                plot.GetArrayElementAtIndex(i).vector2Value = output[i];
            }
            serializedObject.ApplyModifiedProperties();
        }

        void OnUndoRedo()
        {
            Plot();
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
                    path.GetArrayElementAtIndex(i).vector2Value = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
                }
                isReCalcRequired = true;
            }
            EditorGUILayout.PropertyField(path, new GUIContent("Path Data"), true);
            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            if (tg.isShowUserControl)
            {
                serializedObject.Update();
                for (int i = 0; i < path.arraySize; i++)
                {
                    Handles.color = Color.white;
                    var pos = path.GetArrayElementAtIndex(i);
                    EditorGUI.BeginChangeCheck();
                    var changed = Handles.Slider2D((Vector3)pos.vector2Value + tg.transform.position, Vector3.back, Vector3.right, Vector3.up, 0.03f, Handles.CircleHandleCap, 0.5f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(target, "Move Path");
                        pos.vector2Value = changed - tg.transform.position;
                        isReCalcRequired = true;
                    }
                    if (i == 0 || i == path.arraySize - 1)
                        Handles.DrawWireDisc((Vector3)pos.vector2Value + tg.transform.position, Vector3.back, 0.01f);
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
                for (int i = tg.isLoop || len < 3 ? 0 : 1; i < (tg.isLoop || len < 3 ? len : (len - 1)); i++)
                {
                    Handles.color = i % 3 == 0 ? Color.magenta : i % 3 == 1 ? Color.yellow : Color.cyan;
                    Handles.color -= new Color(0, 0, 0, 0.5f);
                    Handles.DrawLine((Vector3)cs[i, 0] + tg.transform.position, (Vector3)cs[i, 1] + tg.transform.position);
                    Handles.DrawLine((Vector3)cs[i, 1] + tg.transform.position, (Vector3)cs[i, 2] + tg.transform.position);
                    Handles.DrawSolidRectangleWithOutline(new Rect { size = Vector2.one * 0.02f, center = (Vector3)cs[i, 0] + tg.transform.position }, new Color(1, 1, 1, 0.2f), Color.white);
                    Handles.DrawSolidRectangleWithOutline(new Rect { size = Vector2.one * 0.02f, center = (Vector3)cs[i, 1] + tg.transform.position }, new Color(1, 1, 1, 0.2f), Color.white);
                    Handles.DrawSolidRectangleWithOutline(new Rect { size = Vector2.one * 0.02f, center = (Vector3)cs[i, 2] + tg.transform.position }, new Color(1, 1, 1, 0.2f), Color.white);
                }
            }
        }



        [MenuItem("Tools/KCurves/PerformanceTest2D")]
        static void _()
        {
            int n = 30;
            int iter = 10;
            int loop = 10000;

            var path = new bool[n].Select(_ => new Vector2(Random.value, Random.value)).ToArray();
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