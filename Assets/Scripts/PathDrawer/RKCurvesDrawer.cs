using System.Collections.Generic;
using UnityEngine;
using RUtil.Curve;
using RUtil.Graphics;
using UnityEngine.Rendering;
using System.Linq;
using static RUtil.Curve.RKCurves;

public class RKCurvesDrawer : IPathDrawer
{
    public int Id { get; set; }
    CurveData Path { get; set; }
    public bool IsDirty { get; private set; } = true;
    public int Length => Path.ValidUserControlLength;
    public bool IsVisible { get; set; } = true;
    public int PlotStepPerSegment => Path.PlotStepPerSegment;

    List<Vector3> controlPolygon;
    Mesh pathMesh;
    Mesh polygonMesh;
    List<(Mesh back, Mesh front)> ucMeshes;
    List<Mesh> bcMeshes;
    List<Mesh> weightTextMeshes;
    TextGenerator tgen;

    List<float> curvatures;
    List<(Vector3, Vector3, float)> curvatureLines;
    Mesh curvatureMesh;
    //Mesh[] fociMeshes;
    //Vector3[] foci;
    //Mesh axisMesh;

    #region static
    static MaterialPropertyBlock prop;
    #endregion

    #region Construct/Export
    public RKCurvesDrawer(CurveData path, int id)
    {
        Id = id;
        Path = path ?? new CurveData();
        controlPolygon = new List<Vector3>();
        pathMesh = new Mesh();
        polygonMesh = new Mesh();
        ucMeshes = new List<(Mesh back, Mesh front)>();
        bcMeshes = new List<Mesh>();
        weightTextMeshes = new List<Mesh>();
        tgen = new TextGenerator();
        curvatures = new List<float>();
        curvatureLines = new List<(Vector3, Vector3, float)>();
        curvatureMesh = new Mesh();
        //fociMeshes = new Mesh[2] { new Mesh(), new Mesh() };
        //foci = new Vector3[2];
        //axisMesh = new Mesh();
    }
    public RKCurvesDrawer() : this(default, -1) { }
    public RKCurvesDrawer(int id = -1) : this(new CurveData(), id) { }
    public RKCurvesDrawer(MinimumPathData data) : this(new CurveData(), data.Id)
    {
        Load(data);
    }
    public MinimumPathData ToMinimumData() => new MinimumPathData(this);
    public void Load(MinimumPathData data)
    {
        Id = data.Id;
        for (int i = 0; i < data.Path.Length; i++)
        {
            Path.AddControl(data.GetCP(i));
        }
        Path.IsClosed = data.IsClosed;
    }

    public IEnumerable<Vector3> GetPlotData() => Path.Plots.Take(Path.ValidPlotLength);

    public IEnumerable<Vector3> GetPolygonPlotData() => Path.ControlPolygon.Skip(IsClosed?0:2).Take(Path.ValidBezierControlLength-(IsClosed?0:4)).Select(x => x.Position);

    public IEnumerable<(Vector3 from, Vector3 to, float width)> GetCurvaturePlotData() => curvatureLines;

    #endregion

    #region Edit
    public void AddControl(ControlPoint control)
    {
        Path.AddControl(control);
        IsDirty = true;
    }
    public void InsertControl(int index, ControlPoint control)
    {
        Path.InsertControl(index, control);
        IsDirty = true;
    }
    public void RemoveControlAt(int index)
    {
        Path.RemoveControlAt(index);
        IsDirty = true;
    }
    public void RemoveLastControl()
    {
        Path.RemoveLastControl();
        IsDirty = true;
    }
    public void Clear()
    {
        Path.Clear();
        IsDirty = true;
    }
    public ControlPoint GetCP(int index)
    {
        return Path.GetCP(index);
    }
    public Vector3 GetPosition(int index)
    {
        return Path.GetPosition(index);
    }
    public float GetWeight(int index)
    {
        return Path.GetWeight(index);
    }
    public void SetPosition(int index, Vector3 pos)
    {
        if (Path.SetPosition(index, pos))
            IsDirty = true;
    }
    public void SetWeight(int index, float weight)
    {
        if (Path.SetWeight(index, weight))
            IsDirty = true;
    }
    public bool IsClosed => Path.IsClosed;
    public void SetClosed(bool isClosed)
    {
        if(isClosed != Path.IsClosed)
        {
            Path.IsClosed = isClosed;
            IsDirty = true;
        }
    }
    #endregion

    public void RecalcMesh(Camera cam, bool includeCurvature)
    {
        if (Path.SegmentCount > 2)
        {
            //BezierPolygon
            if(IsClosed)
                MeshUtil.Make2DPathMesh(polygonMesh, controlPolygon, 0, Path.ValidBezierControlLength+1, 0.005f * cam.orthographicSize);
            else
                MeshUtil.Make2DPathMesh(polygonMesh, controlPolygon, 1, Path.ValidBezierControlLength-3, 0.005f * cam.orthographicSize);

            //BCP
            for (int i = 0; i < Length; i++)
                MeshUtil.Make2DDiscMesh(bcMeshes[i], Path[i, 1], 0.005f * cam.orthographicSize, 8);
        }

        //Path
        MeshUtil.Make2DPathMesh(pathMesh, Path.Plots, 0, Path.ValidPlotLength, 0.01f * cam.orthographicSize);

        //UCP
        for (int i = 0; i < Length; i++)
        {
            MeshUtil.Make2DDiscMesh(ucMeshes[i].back, Path[i], 0.02f * cam.orthographicSize, 8);
            MeshUtil.Make2DDiscMesh(ucMeshes[i].front, Path[i], 0.014f * cam.orthographicSize, 8);
        }

        //Weight Text
        if(Path.SegmentCount > 2)
        {
            for (int i = 0; i < Length; i++)
            {
                Vector3 dir = !IsClosed && i == 0 ? Path[0] - Path[1]
                    : !IsClosed && i == Length - 1 ? Path[Length - 1] - Path[Length - 2]
                    : Path[i] - Path[(i + 1) % Length] + Path[i] - Path[(i + Length - 1) % Length];
                dir = dir.normalized;
                var angle = Vector3.SignedAngle(Vector3.down, dir, Vector3.forward);
                var weight = Mathf.Round(Path[i,1].ModifiedWeight * 100) / 100;
                var weightText = weight >= RKCurves.InfinityWeight ? " " : weight.ToString();
                MeshUtil.MakeTextMesh(
                    weightTextMeshes[i],
                    tgen,
                    $"{weightText}",
                    DrawingCanvas.Instance.Font,
                    24,
                    TextAnchor.MiddleCenter,
                    Path[i].Position + dir * 0.06f * cam.orthographicSize,
                    Color.blue,
                    0.002f * cam.orthographicSize,
                    new Vector3(0, 0, angle > 90 || angle < -90 ? 180 + angle : angle));
            }
        }


        //Curvature
        if (Length>= 3 && includeCurvature)
        {
            curvatureLines.Clear();
            int begin = 1;
            int finish = IsClosed ? (Length + 1) : (Length - 1);
            Vector3 prevP2 = default;
            for (int i = begin; i < finish; i++)
            {

                var len = i == finish - 1 ? Path.PlotStepPerSegment + 1 : Path.PlotStepPerSegment;
                if (Path[i % Path.ValidUserControlLength].TargetWeight >= RKCurves.InfinityWeight)
                {
                    prevP2 = Path.Plots[(i - begin) * Path.PlotStepPerSegment + len - 1];
                    continue;
                }
                for (int j = 0; j < len; j++)
                {
                    var p = Path.Plots[(i - begin) * Path.PlotStepPerSegment + j];
                    var k = curvatures[(i - begin) * Path.PlotStepPerSegment + j];

                    var dir = i == finish - 1 && j == len - 1
                        ? (IsClosed ? Path.Plots[1] - p : p - Path.Plots[(i - begin) * Path.PlotStepPerSegment + j - 1])
                        : Path.Plots[(i - begin) * Path.PlotStepPerSegment + j + 1] - p;
                    dir.Normalize();
                    var p2 = p + k * new Vector3(dir.y, -dir.x) * 0.1f * cam.orthographicSize;

                    if (i != begin || j != 0)
                    {
                        curvatureLines.Add((prevP2, p2, 0.005f * cam.orthographicSize));
                    }

                    if (!IsClosed || i != finish - 1 || j != len - 1)
                        curvatureLines.Add((p, p2, 0.005f * cam.orthographicSize));
                    prevP2 = p2;
                }
            }
            MeshUtil.Make2DLinesMesh(curvatureMesh, curvatureLines);
        }
    }

    void EnsureNotDirty(Camera cam, bool forceRecalcMesh, bool includeCurvature)
    {
        if (IsDirty)
        {
            RKCurves.CalcQNewtonInitMethod = DrawingCanvas.Instance.CalcQNewtonInit;
            Path.CalcBezierControls();
            Path.Plot();

            while (controlPolygon.Count < Path.ControlPolygon.Length + 1)
                controlPolygon.Add(default);

            while (ucMeshes.Count < Length)
                ucMeshes.Add((new Mesh(),new Mesh()));
            while (bcMeshes.Count < Length)
                bcMeshes.Add(new Mesh());
            while (weightTextMeshes.Count < Length)
                weightTextMeshes.Add(new Mesh());

            for (int i = 0; i < Path.ControlPolygon.Length; i++)
            {
                controlPolygon[i] = Path.ControlPolygon[i].Position;
            }
            controlPolygon[Path.ControlPolygon.Length] = Path.ControlPolygon[0].Position;

            //Calc curvature
            if (Length >= 3)
            {
                curvatures.Clear();
                int begin = 1;
                int finish = IsClosed ? (Length + 1) : (Length - 1);
                for (int i = begin; i < finish; i++)
                {
                    var len = i == finish - 1 ? Path.PlotStepPerSegment + 1 : Path.PlotStepPerSegment;
                    for (int j = 0; j < len; j++)
                    {
                        var t = (float)j / Path.PlotStepPerSegment;
                        var p = Path.Plots[(i - begin) * Path.PlotStepPerSegment + j];
                        var k = RKCurves.CalcCurvature(Path[i % Length, 0], Path[i % Length, 1], Path[i % Length, 2], Path[i%Length,1].ModifiedWeight, t);

                        curvatures.Add(k);
                    }
                }
            }

            RecalcMesh(cam, includeCurvature);

            IsDirty = false;
        }
        else if (forceRecalcMesh)
        {
            RecalcMesh(cam, includeCurvature);
        }
    }

    public void Render(Camera cam, CommandBuffer buffer, Material mat, ConicPathDrawerOption options)
    {
        if (prop == null)
            prop = new MaterialPropertyBlock();

        if (!IsVisible)
            return;


        EnsureNotDirty(cam, options.NeedRecalcMesh, options.NeedCurvature);

        if (Path.SegmentCount > 2)
        {
            //ベジェコントロールポリゴン
            if (options.NeedPolygon)
            {
                prop.SetColor("_Color", new Color(1, 0, 1, 0.3f));
                MeshUtil.DrawMesh(polygonMesh, buffer, mat, prop);
            }
            //ベジェ制御点
            if (options.NeedBezierCP)
            {
                for (int i = 0; i < Length; i++)
                {
                    prop.SetColor("_Color", Color.blue);
                    MeshUtil.DrawMesh(bcMeshes[i], buffer, mat, prop);
                }
            }
            //曲率
            if (options.NeedCurvature)
            {
                prop.SetColor("_Color", new Color(0.5f, 0, 0.5f, 0.2f));
                MeshUtil.DrawMesh(curvatureMesh, buffer, mat, prop);
            }

            /*
            (foci[0],foci[1]) = Path.GetFoci(1);
            for(int i=0; i< (Path.GetWeight(1)==1 ? 1 : 2); i++)
            {
                if (!float.IsInfinity(foci[i].x))
                {
                    GraphicMgr.Make2DDiscMesh(fociMeshes[i], foci[i], 0.1f, 8);
                    prop.SetColor("_Color", new Color(1, 0, 1, 0.2f));
                    GraphicMgr.DrawMesh(fociMeshes[i], buffer, mat, prop);
                }
            }
            var axis = Path.GetAxis(1);
            GraphicMgr.Make2DLineMesh(axisMesh, axis.vertex, axis.vertex + axis.dir * 10, 0.02f);
            prop.SetColor("_Color", new Color(1,0,1,0.2f));
            GraphicMgr.DrawMesh(axisMesh, buffer, mat, prop);
            */
        }
        //曲線
        prop.SetColor("_Color", options.NeedPathColorChangeOnHover && options.IsHovered ? Color.blue : Color.black);
        MeshUtil.DrawMesh(pathMesh, buffer, mat, prop);


        //ユーザ制御点
        if (options.NeedCP)
        {
            Color cpcol;
            for (int i = 0; i < Length; i++)
            {
                cpcol = ColorUtil.Jet(Mathf.Log10(Path[i,1].ModifiedWeight) / 3 + 0.5f);

                if (options.NeedCPColorChangeOnHover && options.IsHovered && options.HoveredCP == i)
                {
                    prop.SetColor("_Color", Color.red);
                    MeshUtil.DrawMesh(ucMeshes[i].back, buffer, mat, prop);
                    prop.SetColor("_Color", cpcol);
                    MeshUtil.DrawMesh(ucMeshes[i].front, buffer, mat, prop);
                }
                else
                {
                    prop.SetColor("_Color", new Color(cpcol.r / 2, cpcol.g / 2, cpcol.b / 2));
                    MeshUtil.DrawMesh(ucMeshes[i].back, buffer, mat, prop);
                    prop.SetColor("_Color", cpcol);
                    MeshUtil.DrawMesh(ucMeshes[i].front, buffer, mat, prop);
                }
            }
        }

        if (options.NeedWeightText)
        {
            var begin = IsClosed ? 0 : 1;
            var finish = IsClosed ? Length : Length - 1;
            for (int i = begin; i < finish; i++)
            {
                buffer.DrawMesh(weightTextMeshes[i], Matrix4x4.identity, DrawingCanvas.Instance.Font.material);
            }
        }

    }

    public bool IsOnPath(Camera cam, Vector3 v, out float distance, out int segment, out bool isAtSideP, bool updateCurvature)
    {
        EnsureNotDirty(cam, false, updateCurvature);
        return Path.IsOnPath(v, 0.02f * cam.orthographicSize, out distance, out segment, out isAtSideP);
    }
}

public struct ConicPathDrawerOption
{
    public bool NeedRecalcMesh;
    public bool NeedCP;
    public bool NeedBezierCP;
    public bool NeedPolygon;
    public bool NeedPathColorChangeOnHover;
    public bool NeedCPColorChangeOnHover;
    public bool NeedWeightText;
    public bool NeedCurvature;
    public bool IsHovered;
    public int HoveredCP;
    public int HoveredSegment;
    public bool IsHoveredAtSideP;
}

[System.Serializable]
public class MinimumCanvasData
{
    public List<MinimumPathData> Paths;

    public MinimumCanvasData() { Paths = new List<MinimumPathData>(); }

    public MinimumCanvasData(IEnumerable<IPathDrawer> drawers)
    {
        Paths = new List<MinimumPathData>(drawers.Count());
        foreach(var d in drawers)
        {
            Paths.Add(d.ToMinimumData());
        }
    }
}