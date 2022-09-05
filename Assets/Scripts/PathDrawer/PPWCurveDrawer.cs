using System.Collections.Generic;
using UnityEngine;
using RUtil.Curve;
using RUtil.Graphics;
using UnityEngine.Rendering;
using System.Linq;


public class PPWCurveDrawer : IPathDrawer
{
    public int Id { get; set; }
    PPWCurve.CurveData Path { get; set; }
    public bool IsDirty { get; private set; } = true;
    public void SetDirty() => IsDirty = true;
    public int Length => Path.ValidCPCount;
    public int PlotStepPerSegment => Path.PlotStepPerSegment;
    public bool IsVisible { get; set; } = true;
    public PPWCurve.BlendingScheme Scheme = PPWCurve.BlendingScheme.Hyperbolic_Extended;

    Mesh pathMesh;
    List<(Mesh back, Mesh front)> ucMeshes;
    List<Mesh> tmpPlotMeshes;
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
    public PPWCurveDrawer(PPWCurve.CurveData path, int id)
    {
        Id = id;
        Path = path ?? new PPWCurve.CurveData(3, 20);
        Path.Init(path?.ControlPoints ?? new List<ControlPoint>(), path?.Phis ?? new List<float>(), path?.Psis ?? new List<float>(), 50);
        pathMesh = new Mesh();
        ucMeshes = new List<(Mesh back, Mesh front)>();
        tmpPlotMeshes = new List<Mesh>();
        weightTextMeshes = new List<Mesh>();
        tgen = new TextGenerator();
        curvatures = new List<float>();
        curvatureLines = new List<(Vector3, Vector3, float)>();
        curvatureMesh = new Mesh();
        //fociMeshes = new Mesh[2] { new Mesh(), new Mesh() };
        //foci = new Vector3[2];
        //axisMesh = new Mesh();
    }
    public PPWCurveDrawer() : this(default, -1) { }
    public PPWCurveDrawer(int id = -1) : this(default, id) { }
    public PPWCurveDrawer(MinimumPathData data) : this(default, default)
    {
        Load(data);
    }
    public MinimumPathData ToMinimumData() => new MinimumPathData(this);
    public void Load(MinimumPathData data)
    {
        Id = data.Id;
        var autoupdate = Path.NeedAutoPlot;
        Path.NeedAutoPlot = false;
        for (int i = 0; i < data.Path.Length; i++)
        {
            Path.AddControl(data.GetCP(i));
            Path.SetPhi(i, data.Weights[data.Path.Length + i]);
            Path.SetPsi(i, data.Weights[data.Path.Length * 2 + i]);
        }
        Path.IsClosed = data.IsClosed;
        Path.NeedAutoPlot = autoupdate;
    }

    public IEnumerable<Vector3> GetPlotData() => IsClosed ? Path.Plots.Take(Path.ValidPlotLength).Append(Path.Plots[0]) : Path.Plots.Take(Path.ValidPlotLength);        

    public IEnumerable<(Vector3 from, Vector3 to, float width)> GetCurvaturePlotData() => curvatureLines;

    public IEnumerable<IEnumerable<Vector3>> GetTmpPlotData() => Path.Polygons.Take(Length)
        .Select((path, i) => i==0 ? path.Skip(Path.PlotStepPerSegment) : i==Length-1 ? path.Take(Path.PlotStepPerSegment) : path);

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
    public float GetPhi(int index)
    {
        return Path.GetPhi(index);
    }
    public float GetPsi(int index)
    {
        return Path.GetPsi(index);
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
    public void SetPhi(int index, float phi)
    {
        if (Path.SetPhi(index, phi))
            IsDirty = true;
    }

    public void SetPsi(int index, float phi)
    {
        if (Path.SetPsi(index, phi))
            IsDirty = true;
    }
    public bool IsClosed => Path.IsClosed;
    public void SetClosed(bool isClosed)
    {
        if (isClosed != Path.IsClosed)
        {
            Path.IsClosed = isClosed;
            IsDirty = true;
        }
    }
    #endregion

    public void RecalcMesh(Camera cam, bool includeCurvature)
    {
        //Path
        if (Path.ValidPlotLength >= 2)
            MeshUtil.Make2DPathMesh(pathMesh, Path.Plots, 0, Path.ValidPlotLength, 0.01f * cam.orthographicSize, Path.IsClosed);

        //CP
        for (int i = 0; i < Length; i++)
        {
            MeshUtil.Make2DDiscMesh(ucMeshes[i].back, Path[i], 0.02f * cam.orthographicSize, 8);
            MeshUtil.Make2DDiscMesh(ucMeshes[i].front, Path[i], 0.014f * cam.orthographicSize, 8);
        }

        //Weight Text
        if (Path.SegmentCount > 2)
        {
            for (int i = 0; i < Length; i++)
            {
                Vector3 dir = !IsClosed && i == 0 ? Path[0] - Path[1]
                    : !IsClosed && i == Length - 1 ? Path[Length - 1] - Path[Length - 2]
                    : Path[i] - Path[(i + 1) % Length] + Path[i] - Path[(i + Length - 1) % Length];
                dir = dir.normalized;
                var angle = Vector3.SignedAngle(Vector3.down, dir, Vector3.forward);
                var weight = Mathf.Round(Path[i].TargetWeight * 100) / 100;
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

        //Polygon
        if (Path.SegmentCount >= 2)
        {
            for (int i = 0; i < Length; i++)
            {
                MeshUtil.Make2DPathMesh(tmpPlotMeshes[i], Path.Polygons[i],
                    !Path.IsClosed && i==0 ? Path.PlotStepPerSegment : 0, 
                    !Path.IsClosed && (i==0 || i==Length-1) ? Path.Polygons[i].Count/2 + 1 : Path.Polygons[i].Count,
                    0.01f * cam.orthographicSize);
            }
        }

        //Curvature
        if (Length >= 3 && includeCurvature)
        {
            curvatureLines.Clear();

            Vector3 prevP2 = default;

            for (int i=0; i<Path.ValidPlotLength; i++)
            {
                var p = Path.Plots[i];
                var np = Path.Plots[(i + 1) % Path.ValidPlotLength];
                var pp = Path.Plots[(i + Path.ValidPlotLength - 1) % Path.ValidPlotLength];
                var k = curvatures[i];
                Vector3 dir = ((np - p) + (p - pp)) / 2;
                if (i == 0 && !Path.IsClosed) dir = np - p;
                else if (i == Path.ValidPlotLength - 1 && !Path.IsClosed) dir = p - pp;
                dir.Normalize();
                var p2 = p + k * new Vector3(dir.y, -dir.x) * 0.1f * cam.orthographicSize;
                if(i!=0)
                    curvatureLines.Add((prevP2, p2, 0.005f * cam.orthographicSize));
                curvatureLines.Add((p, p2, 0.005f * cam.orthographicSize));
                prevP2 = p2;
            }
            if (Path.IsClosed)
            {
                var p = Path.Plots[0];
                var np = Path.Plots[1];
                var pp = Path.Plots[Path.ValidPlotLength - 1];
                var k = curvatures[0];
                Vector3 dir = ((np - p) + (p - pp)) / 2;
                dir.Normalize();
                var p2 = p + k * new Vector3(dir.y, -dir.x) * 0.1f * cam.orthographicSize;
                curvatureLines.Add((prevP2, p2, 0.005f * cam.orthographicSize));
            }

            MeshUtil.Make2DLinesMesh(curvatureMesh, curvatureLines);
        }
    }

    void EnsureNotDirty(Camera cam, bool forceRecalcMesh, bool includeCurvature)
    {
        if (IsDirty)
        {
            PPWCurve.CalcQNewtonInit = DrawingCanvas.Instance.CalcQNewtonInit;
            if (DrawingCanvas.Instance.ReferEachBlendingScheme)
            {
                PPWCurve.Scheme = Scheme;
            }
            else
            {
                PPWCurve.Scheme = DrawingCanvas.Instance.BlendingScheme;
            }
            PPWCurve.CalcQMethod = DrawingCanvas.Instance.CalcQMethod;
            Path.EnsureAllocated();
            Path.Plot();


            while (ucMeshes.Count < Length)
                ucMeshes.Add((new Mesh(), new Mesh()));
            while (tmpPlotMeshes.Count < Length)
                tmpPlotMeshes.Add(new Mesh());
            while (weightTextMeshes.Count < Length)
                weightTextMeshes.Add(new Mesh());


            //Calc curvature
            if (Length >= 3)
            {
                curvatures.Clear();
                int plotCount = Path.ValidPlotLength;
                for (int i = 0; i < plotCount; i++)
                {
                    Vector2 pp = Path.Plots[(i + plotCount - 1) % plotCount];
                    Vector2 p = Path.Plots[i];
                    Vector2 np = Path.Plots[(i + 1) % plotCount];
                    var k = CalcCurvature(pp, p, np);
                    curvatures.Add(k);
                }

                if (!Path.IsClosed)
                {
                    curvatures[0] = curvatures[1];
                    curvatures[plotCount - 1] = curvatures[plotCount - 2];
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

    float CalcCurvature(Vector2 a, Vector2 b, Vector2 c)
    {
        var cx = ((a.y - b.y) * (c.sqrMagnitude - a.sqrMagnitude) - (a.y - c.y) * (b.sqrMagnitude - a.sqrMagnitude))
            / (2 * (a.x - b.x) * (a.y - c.y) - 2 * (a.x - c.x) * (a.y - b.y));
        var cy = ((a.x - c.x) * (b.sqrMagnitude - a.sqrMagnitude) - (a.x - b.x) * (c.sqrMagnitude - a.sqrMagnitude))
            / (2 * (a.x - b.x) * (a.y - c.y) - 2 * (a.x - c.x) * (a.y - b.y));
        return Mathf.Sign((b - a).x * (c - b).y - (c - b).x * (b - a).y) / Vector2.Distance(new Vector2(cx, cy), a);
    }


    public void Render(Camera cam, CommandBuffer buffer, Material mat, ConicPathDrawerOption options)
    {
        if (prop == null)
            prop = new MaterialPropertyBlock();

        if (!IsVisible)
            return;


        EnsureNotDirty(cam, options.NeedRecalcMesh, options.NeedCurvature);

        if (Path.SegmentCount >= 2)
        {
            //制御ポリゴン
            if (options.NeedPolygon)
            {
                for (int i = 0; i < Length; i++)
                {
                    prop.SetColor("_Color", new Color(i % 2, 0, 1 - i % 2, 0.1f));
                    MeshUtil.DrawMesh(tmpPlotMeshes[i], buffer, mat, prop);
                }
            }
            //曲率
            if (options.NeedCurvature)
            {
                prop.SetColor("_Color", new Color(0.5f, 0, 0.5f, 0.2f));
                MeshUtil.DrawMesh(curvatureMesh, buffer, mat, prop);
            }
        }
        //曲線
        prop.SetColor("_Color", options.NeedPathColorChangeOnHover && options.IsHovered ? Color.blue : Color.black);
        MeshUtil.DrawMesh(pathMesh, buffer, mat, prop);


        //制御点
        if (options.NeedCP)
        {
            Color cpcol;
            for (int i = 0; i < Length; i++)
            {
                cpcol = ColorUtil.Jet(Mathf.Log10(Path[i].TargetWeight) / 3 + 0.5f);

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

        //重み数値
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


    /// <summary>
    /// 点が曲線上にあるかどうか
    /// </summary>
    public bool IsOnPath(Camera cam, Vector3 v, out float distance, out int segment, out bool isAtSideP, bool updateCurvature)
    {
        bool IsOnLine(Vector3 from, Vector3 to, float _error, out float _distance)
        {
            var dir = (to - from).normalized;
            var (a, b) = (from, to);
            if (Mathf.Approximately(Vector3.Distance(a, b), 0))
            {
                _distance = Vector3.Distance(a, v);
                return _distance < _error;
            }
            if (Vector3.Dot(v - a, dir) >= -_error && Vector3.Dot(v - b, -dir) >= -_error)
            {
                _distance = Mathf.Sqrt(Vector3.Cross(a - v, b - v).sqrMagnitude / (a - b).sqrMagnitude);
                return _distance < _error;
            }
            _distance = -1;
            return false;
        }

        EnsureNotDirty(cam, false, updateCurvature);

        var error = 0.02f * cam.orthographicSize;

        distance = -1;
        segment = -1;
        isAtSideP = false;
        if (Path.ValidCPCount == 0)
            return false;

        if (Path.ValidCPCount == 1)
        {
            distance = Vector3.Distance(Path[0], v);
            segment = 0;
            return distance < error;
        }

        if (Path.ValidCPCount == 2)
        {
            segment = 0;
            isAtSideP = true;
            return IsOnLine(Path[0], Path[1], error, out distance);
        }

        Vector3 current = Path.Plots[0];
        segment = 0;
        for(int i=1; i<Path.ValidPlotLength; i++)
        {
            var dist = Vector3.Distance(current, Path.Plots[i]);
            if (dist < error)
            {
                if (i % Path.PlotStepPerSegment == 0)
                    segment++;

                continue;
            }
            if (IsOnLine(current, Path.Plots[i], error, out distance))
            {
                isAtSideP = i % Path.PlotStepPerSegment < Path.PlotStepPerSegment / 2;
                return true;
            }

            current = Path.Plots[i];
            if (i % Path.PlotStepPerSegment == 0)
                segment++;
        }

        return false;
    }
}

