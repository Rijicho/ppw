using UnityEngine;
using RUtil.Curve;
using static RUtil.Curve.RKCurves;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RKCurvesViewer : MonoBehaviour
{
    public LineRenderer rdr;
    public ControlPoint[] path = new[] { ControlPoint.zero, new ControlPoint(0,1) };
    public Vector3[] plot = default;
    public bool isLoop = true;
    public bool isShowUserControl = true;
    public bool isShowWeightControl = true;
    public bool isShowBezierControl = false;
    public bool isShowCurvature = false;
    public int stepPerSegment = 20;
    public int loopCount = 10;
    public float globalW = 1;
    public float LineWidth = 1f;
    public Color LineColor = Color.green;
    public Color CPColor = Color.white;
    public Color KappaColor = new Color(0.3f, 0, 0.3f);


    public ControlPoint[] Path { get => path; set => path = value; }
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
                Handles.color = LineColor;// i % 2 == 0 ? Color.green : new Color(0, 0.5f, 0);
                Handles.DrawAAPolyLine(LineWidth, plot[i] + transform.position, plot[(i + 1) % plot.Length] + transform.position);
            }
        }
    }
#endif
}