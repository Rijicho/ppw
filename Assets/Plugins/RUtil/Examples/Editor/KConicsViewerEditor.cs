using System.Linq;
using UnityEngine;
using UnityEditor;
using RUtil.Editor;
using static RUtil.Curve.RKCurves;
using RUtil.Curve;

[CustomEditor(typeof(RKCurvesViewer))]
public class KConicsViewerEditor : CustomInspector<KConicsViewerEditor.P>
{
    public enum P
    {
        rdr,
        path,
        plot,
        isLoop,
        isShowUserControl,
        isShowBezierControl,
        isShowWeightControl,
        isShowCurvature,
        stepPerSegment,
        loopCount,
        globalW,
        LineWidth,
        LineColor,
        CPColor,
        KappaColor,
    }

    RKCurvesViewer tg;

    protected override void OnEnable()
    {
        base.OnEnable();
        tg = target as RKCurvesViewer;
        Undo.undoRedoPerformed += OnUndoRedo;

        isReCalcRequired = true;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnUndoRedo()
    {
        Plot();
    }

    BezierControls controls = new BezierControls(8);
    Vector3[] output = new Vector3[256];

    void Plot()
    {
        if (tg.path == null)
            return;

        CalcBezierControls(tg.path, tg.loopCount, tg.isLoop, controls);

        var plotLength = CalcPlots(controls, tg.isLoop, ref output, tg.stepPerSegment);


        serializedObject.Update();
        V[P.plot].arraySize = plotLength;
        for (int i = 0; i < plotLength; i++)
        {
            V[P.plot].GetArrayElementAtIndex(i).vector3Value = output[i];
        }
        serializedObject.ApplyModifiedProperties();
    }


    bool isReCalcRequired;
    bool prevIsLoop;
    int prevPathCount;
    int prevStepPerSegment;
    int prevLoopCount;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        PF(P.rdr, "Renderer");
        PF(P.LineWidth, "Line Width");
        PF(P.LineColor, "Line Color");
        PF(P.CPColor, "CP Color");
        PF(P.KappaColor, "Curvature Color");
        PF(P.isLoop, "Close Path");
        PF(P.loopCount, "Iteration");
        PF(P.stepPerSegment, "Density of Plot");
        if (V[P.stepPerSegment].intValue <= 0)
            V[P.stepPerSegment].intValue = 1;
        PF(P.isShowUserControl, "Show Control Points");
        PF(P.isShowWeightControl, "Show Weight Controls");
        PF(P.isShowBezierControl, "Show Bezier Controls");
        PF(P.isShowCurvature, "Show Curvature");


        PFArray(P.path, "Control Points");

        PF(P.globalW, "Global Weight");
        if(V[P.globalW].floatValue < 0.1f)
        {
            V[P.globalW].floatValue = 0.1f;
        }
        if (serializedObject.hasModifiedProperties)
        {
            isReCalcRequired = true;
        }





        if (GUILayout.Button("Reset Position"))
        {
            for (int i = 0; i < tg.path.Length; i++)
            {
                var theta = Mathf.PI * 2 / tg.path.Length * i;
                V[P.path].GetArrayElementAtIndex(i).FindPropertyRelative("Position").vector3Value = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta));
            }
            isReCalcRequired = true;
        }
        if (GUILayout.Button("Reset Weights"))
        {
            V[P.globalW].floatValue = 1;
            for (int i = 0; i < tg.path.Length; i++)
            {
                V[P.path].GetArrayElementAtIndex(i).FindPropertyRelative("Weight").floatValue = 1;
            }
            isReCalcRequired = true;
        }

        if (GUILayout.Button("Recalc"))
        {
            isReCalcRequired = true;
        }


        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        var path = V[P.path];
        var plot = V[P.plot];

        int movingHandle = -1;
        if (tg.isShowUserControl)
        {
            serializedObject.Update();
            for (int i = 0; i < path.arraySize; i++)
            {
                Handles.color = tg.CPColor;
                var pos = path.GetArrayElementAtIndex(i).FindPropertyRelative("Position");
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

        if (tg.isShowWeightControl && tg.path.Length > 2)
        {
            var labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            serializedObject.Update();

            for (int i = 0; i < path.arraySize; i++)
            {
                if (!tg.isLoop && (i == 0 || i == path.arraySize - 1))
                    continue;
                Handles.color = Color.white;
                var pos = path.GetArrayElementAtIndex(i).FindPropertyRelative("Position");


                EditorGUI.BeginChangeCheck();
                var w = path.GetArrayElementAtIndex(i).FindPropertyRelative("TargetWeight");

                var q = Quaternion.Euler(0, 0, w.floatValue * 180f);
                var id = GUIUtility.GetControlID(FocusType.Passive) + 1;
                var changed = Handles.ScaleSlider(w.floatValue, pos.vector3Value + tg.transform.position, Vector3.up, Quaternion.identity, 0.2f, 0.1f);
                if (GUIUtility.hotControl == id)
                {
                    movingHandle = i;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "Change Weight");
                    w.floatValue = Mathf.Max(changed, 0.1f);
                    isReCalcRequired = true;
                }
                Handles.Label(pos.vector3Value + tg.transform.position + new Vector3(0.02f, 0.25f), $"w={w.floatValue*tg.globalW}", labelStyle);


            }
            serializedObject.ApplyModifiedProperties();
        }

        if (prevIsLoop != tg.isLoop)
        {
            isReCalcRequired = true;
            prevIsLoop = tg.isLoop;
        }
        if (prevPathCount != tg.path.Length)
        {
            isReCalcRequired = true;
            prevPathCount = tg.path.Length;
        }
        if (prevStepPerSegment != tg.stepPerSegment)
        {
            isReCalcRequired = true;
            prevStepPerSegment = tg.stepPerSegment;
        }
        if (prevLoopCount != tg.loopCount)
        {
            isReCalcRequired = true;
            prevLoopCount = tg.loopCount;
        }
        if (plot.arraySize == 0 && path.arraySize > 0)
        {
            isReCalcRequired = true;
        }

        if (isReCalcRequired)
        {
            Plot();
            isReCalcRequired = false;
        }

        if (tg.isShowBezierControl)
        {
            var cs = controls;
            var len = cs.SegmentCount;
            for (int i = tg.isLoop ? 0 : 1; i < (tg.isLoop ? len : (len - 1)); i++)
            {
                Handles.color = i % 3 == 0 ? Color.magenta : i % 3 == 1 ? Color.yellow : Color.cyan;
                Handles.color -= new Color(0, 0, 0, 0.5f);
                Handles.DrawLine(cs[i, 0].Position + tg.transform.position, cs[i, 1].Position + tg.transform.position);
                Handles.DrawLine(cs[i, 1].Position + tg.transform.position, cs[i, 2].Position + tg.transform.position);
                Handles.DrawWireCube(cs[i, 0].Position + tg.transform.position, Vector3.one * 0.02f);
                Handles.DrawWireCube(cs[i, 1].Position + tg.transform.position, Vector3.one * 0.02f);
                Handles.DrawWireCube(cs[i, 2].Position + tg.transform.position, Vector3.one * 0.02f);
            }
        }

        if (tg.isShowCurvature)
        {
            var cs = controls;
            var len = controls.SegmentCount;
            int pi = 0;
            Handles.color = new Color(0, 0.5f, 0.5f, 0.8f);
            for (int s=tg.isLoop ? 0 : 1; s < (tg.isLoop ? len : (len-1)); s++)
            {
                var (p, q, r) = (cs[s, 0], cs[s, 1], cs[s, 2]);
                var w = tg.path[s].TargetWeight;
                for (int i = 0; i < tg.stepPerSegment; i++)
                {
                    var t = (float)i / tg.stepPerSegment;
                    var x = tg.plot[pi++];
                    Vector3 nx;
                    nx = tg.plot[pi];
                    var k = Mathf.Abs(RKCurves.CalcCurvature(p, q, r, w, t));
                    var d = (nx - x).normalized;
                    var n = Quaternion.AngleAxis(90, Vector3.Cross(p - q, r - q).normalized) * d;
                    Handles.DrawLine(tg.transform.position + x, tg.transform.position + x +  n * k);
                }
            }
        }

        if (movingHandle >= 0)
        {
            if (!tg.isLoop)
            {
                if (movingHandle == 0)
                    movingHandle = 1;
                else if (movingHandle == tg.path.Length - 1)
                    movingHandle = tg.path.Length - 2;
            }

            var (P, Q, R, L) = (controls[movingHandle, 0].Position, controls[movingHandle, 1].Position, controls[movingHandle, 2].Position, tg.path[movingHandle].Position);

            var vp = P - Q;
            var vr = R - Q;
            var vq = P - R;
            var (vps, vqs, vrs) = (vp.sqrMagnitude, vq.sqrMagnitude, vr.sqrMagnitude);
            var w = tg.path[movingHandle].TargetWeight * tg.globalW;
            var ww = w * w;

            //軸の方程式
            float al0, al1, al2, as0, as1, as2;
            if (vps == vrs)
            {
                if (vps > vqs)
                {
                    al0 = -1;
                    al1 = 0;
                    al2 = 1;
                    as0 = ww;
                    as1 = 1;
                    as2 = ww;
                }
                else
                {
                    as0 = -1;
                    as1 = 0;
                    as2 = 1;
                    al0 = ww;
                    al1 = 1;
                    al2 = ww;
                }
            }
            else
            {
                var a = ww * (ww - 1);
                var b = (2 * ww * (vps + vrs) - vqs) / (vps - vrs);
                var c = 1;
                if (w == 1)
                {
                    as1 = al1 = -c / b;
                }
                else
                {
                    var sign = Mathf.Sign(vps - vrs);
                    al1 = (-b + sign * Mathf.Sqrt(b * b - 4 * a * c)) / 2 / a;
                    as1 = (-b - sign * Mathf.Sqrt(b * b - 4 * a * c)) / 2 / a;
                }
                al0 = al1 * ww - 1;
                al2 = al1 * ww + 1;
                as0 = as1 * ww - 1;
                as2 = as1 * ww + 1;
            }

            //軸との交点
            var wsign = Mathf.Sign(w);
            var rootl = wsign * Mathf.Sqrt(al1 * al1 - al0 * al2 / ww);
            var roots = wsign * Mathf.Sqrt(as1 * as1 - as0 * as2 / ww);
            var kl1 = new Vector3(
                (-al1 - rootl) / 2 / al0,
                1,
                (-al1 + rootl) / 2 / al2);
            var kl2 = new Vector3(
                (-al1 + rootl) / 2 / al0,
                1,
                (-al1 - rootl) / 2 / al2);
            var ks1 = new Vector3(
                (-as1 - roots) / 2 / as0,
                1,
                (-as1 + roots) / 2 / as2);
            var ks2 = new Vector3(
                (-as1 + roots) / 2 / as0,
                1,
                (-as1 - roots) / 2 / as2);


            //交点を元の座標へ変換
            kl1 = tg.transform.position + BackCoord(kl1, P, Q, R);
            kl2 = tg.transform.position + BackCoord(kl2, P, Q, R);
            ks1 = tg.transform.position + BackCoord(ks1, P, Q, R);
            ks2 = tg.transform.position + BackCoord(ks2, P, Q, R);

            //軸を描画
            if (w == 1)
            {
                var dir = (vp + vr).normalized * 5;
                Handles.color = new Color(1, 1, 1, 0.2f);
                Handles.DrawLine(kl1 - dir, kl1 + dir);
            }
            else
            {
                Handles.color = new Color(1, 0.5f, 0.5f, 0.2f);
                Handles.DrawLine(kl1, kl2);
                Handles.color = new Color(0.5f, 0.5f, 1, 0.2f);
                Handles.DrawLine(ks1, ks2);
            }

            //頂点を描画
            Handles.color = Color.white;
            Handles.FreeMoveHandle(kl1, Quaternion.identity, 0.02f, default, Handles.SphereHandleCap);
            Handles.FreeMoveHandle(kl2, Quaternion.identity, 0.02f, default, Handles.SphereHandleCap);
            Handles.FreeMoveHandle(ks1, Quaternion.identity, 0.02f, default, Handles.SphereHandleCap);
            Handles.FreeMoveHandle(ks2, Quaternion.identity, 0.02f, default, Handles.SphereHandleCap);

            //中心を描画
            var center = new Vector3(0.5f, -ww, 0.5f);
            center = tg.transform.position + BackCoord(center, P, Q, R);
            Handles.color = Color.cyan;
            Handles.FreeMoveHandle(center, default, 0.02f, default, Handles.SphereHandleCap);

            //w=-w部分を描画
            Handles.color = new Color(1, 1, 1, 0.2f);
            Vector3 prevPoint = CalcPlotSingle(P, Q, R, 0, -w);
            for (int i = 1; i <= tg.stepPerSegment; i++)
            {
                var t = (float)i / tg.stepPerSegment;
                var currentPoint = CalcPlotSingle(P, Q, R, t, -w);
                Handles.DrawLine(tg.transform.position + prevPoint, tg.transform.position + currentPoint);
                prevPoint = currentPoint;
            }
        }
    }

    static Vector3 BackCoord(Vector3 v, Vector3 P, Vector3 Q, Vector3 R)
    {
        v /= (v.x + v.y + v.z);
        return v.x * P + v.y * Q + v.z * R;
    }
}