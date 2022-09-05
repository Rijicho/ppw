using Cysharp.Threading.Tasks;
using RUtil;
using RUtil.Curve;
using RUtil.Graphics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEngine.UI;
using System.Linq;
using System.IO;

public enum PathType
{
    KConics,
    GYuksel,
}

public class DrawingCanvas : SingletonMonoBehaviour<DrawingCanvas>
{
    public GameObject Root;
    public Camera Cam;
    public Material LineMaterial;
    public Font Font;
    public Canvas Canvas;
    public CanvasScaler CanvasScaler;
    public RectTransform UIArea;

    public GameObject pPoint;
    public GameObject pLine;

    public PathType CreateMode = PathType.GYuksel;
    public PPWCurve.BlendingScheme BlendingScheme = PPWCurve.BlendingScheme.Hyperbolic_Extended;
    public BezierCurve.CalcQMethod CalcQMethod = BezierCurve.CalcQMethod.Approximated;
    public BezierCurve.CalcQNewtonInit CalcQNewtonInit = BezierCurve.CalcQNewtonInit.Heulistic1;
    public bool ReferEachBlendingScheme = false;
    public bool ColorPPWSegment = false;

    CommandBuffer buffer;

    Dictionary<int, IPathDrawer> drawers { get; } = new Dictionary<int, IPathDrawer>();

    public IReadOnlyDictionary<int, IPathDrawer> Drawers => drawers;

    int maxid = 0;

    public IPathDrawer TempDrawer { get; set; }

    public bool NeedShowCPDefault { get; set; } = true;
    public bool NeedShowWeight { get; set; } = true;
    public bool NeedShowPolygon { get; set; } = true;
    public bool NeedShowCurvature { get; set; }
    public string FileName { get; set; } = "default";


    public Dropdown MethodDropdown;
    public void OnMethodDropdownChanged(int value)
    {
        CalcQMethod = (BezierCurve.CalcQMethod)value;
    }



    bool isRecalcMeshRequired;

    public IPathDrawer GetDrawer(int id)
    {
        return drawers[id];
    }
    public T GetDrawer<T>(int id)
        where T : IPathDrawer, new()
    {
        if (drawers[id] is T d)
            return d;
        else
            throw new System.InvalidCastException($"drawers[{id}]は{typeof(T).Name}ではありません");
    }

    public T AddPath<T>()
        where T: IPathDrawer, new()
    {
        var drawer = new T();
        drawer.Id = ++maxid;
        drawers[drawer.Id] = drawer;
        return drawer;
    }


    public T AddPath<T>(MinimumPathData data, bool getNewId = false)
        where T : IPathDrawer, new()
    {
        var drawer = data.Load();
        if(drawer is T tdrawer)
        {
            tdrawer.Id = getNewId ? ++maxid : tdrawer.Id;
            drawers[tdrawer.Id] = tdrawer;
            return tdrawer;
        }
        else
        {
            throw new System.InvalidCastException("Wrong path type.");
        }
    }


    public IPathDrawer AddPath(MinimumPathData data, bool getNewId = false)
    {
        var drawer = data.Load();
        drawer.Id = getNewId ? ++maxid : drawer.Id;
        drawers[drawer.Id] = drawer;
        return drawer;
    }


    public MinimumPathData RemovePath(int id)
    {
        var data = drawers[id].ToMinimumData();
        drawers.Remove(id);
        return data;
    }

    public void RemoveAllPath()
    {
        drawers.Clear();
    }


    public void AddFan(Vector3 center, float beginDeg, float endDeg, float radius, bool withCenter = true)
    {
        var d = AddPath<RKCurvesDrawer>();

        while (beginDeg > endDeg)
            endDeg += 360;

        var delta = ((endDeg - beginDeg) % 360);

        if (delta == 0)
        {
            if (withCenter)
            {
                d.AddControl(new ControlPoint(center, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg, Vector3.forward) * Vector3.right), RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + 60, Vector3.forward) * Vector3.right), 0.5f));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + 180, Vector3.forward) * Vector3.right), 0.5f));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + 300, Vector3.forward) * Vector3.right), 0.5f));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg, Vector3.forward) * Vector3.right), RKCurves.InfinityWeight));
            }
            else
            {
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg, Vector3.forward) * Vector3.right), 0.5f));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg+120, Vector3.forward) * Vector3.right), 0.5f));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg+240, Vector3.forward) * Vector3.right), 0.5f));
            }
            d.SetClosed(true);
            return;
        }



        if (withCenter)
        {
            d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(endDeg, Vector3.forward) * Vector3.right), RKCurves.InfinityWeight));
            d.AddControl(new ControlPoint(center, RKCurves.InfinityWeight));
            d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg, Vector3.forward) * Vector3.right), RKCurves.InfinityWeight));
        }
        else
        {
            d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg, Vector3.forward) * Vector3.right), 1));
        }


        if (delta * Mathf.Deg2Rad < Mathf.PI)
        {
            var theta = delta / 2;
            var w = Mathf.Cos(theta * Mathf.Deg2Rad);
            if (w > 0.01f)
            {
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + theta, Vector3.forward) * Vector3.right), w));
            }
            else
            {
                theta = delta / 4;
                w = Mathf.Cos(theta * Mathf.Deg2Rad);
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + theta, Vector3.forward) * Vector3.right), w));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + theta * 3, Vector3.forward) * Vector3.right), w));
            }
        }
        else
        {
            var theta = delta / 4;
            var w = Mathf.Cos(theta * Mathf.Deg2Rad);
            if (w > 0.01f)
            {
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + theta, Vector3.forward) * Vector3.right), w));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + theta * 3, Vector3.forward) * Vector3.right), w));
            }
            else
            {
                theta = delta / 6;
                w = Mathf.Cos(theta * Mathf.Deg2Rad);
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + theta, Vector3.forward) * Vector3.right), w));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + theta * 3, Vector3.forward) * Vector3.right), w));
                d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(beginDeg + theta * 5, Vector3.forward) * Vector3.right), w));
            }
        }

        if(!withCenter)
        {
            d.AddControl(new ControlPoint(center + radius * (Quaternion.AngleAxis(endDeg, Vector3.forward) * Vector3.right), 1));
        }
        d.SetClosed(withCenter);
    }


    public PPWCurveDrawer AddFanYuksel(Vector3 center, float beginDeg, float endDeg, float radius, bool setPsiToInf = true)
    {
        var d = AddPath<PPWCurveDrawer>();

        while (beginDeg > endDeg)
            endDeg += 360;

        var theta = ((endDeg - beginDeg) % 360);
        int n = theta == 0 ? 5 : Mathf.Max(3, Mathf.CeilToInt((theta+10) / 90) + 1);
        if (theta % 360 == 0)
        {
            for (int i = 0; i < n; i++)
            {
                d.AddControl(new ControlPoint
                {
                    Position = center + Quaternion.Euler(0, 0, beginDeg + 360 / 5 * i) * Vector3.right * radius,
                    TargetWeight = Mathf.Cos(Mathf.PI * 2 / 5)
                });
                d.SetPhi(i,1);
                if (setPsiToInf)
                    d.SetPsi(i, PPWCurve.PsiInfinity);
                else
                    d.SetPsi(i, 0);
                //d.SetPsi(i, 0);
                d.SetClosed(true);
            }
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                d.AddControl(new ControlPoint
                {
                    Position = center + Quaternion.Euler(0, 0, beginDeg + theta / (n - 1) * i) * Vector3.right * radius,
                    TargetWeight = Mathf.Cos(theta / (n - 1) * Mathf.Deg2Rad)
                });
                d.SetPhi(i, 1);

                if (i == 0)
                {
                    d.SetPsi(i, PPWCurve.PsiInfinity);
                }
                else if (i == n - 2)
                {
                    d.SetPsi(i, -PPWCurve.PsiInfinity);
                }
                else
                {
                    if (setPsiToInf)
                    {
                        d.SetPsi(i, PPWCurve.PsiInfinity);
                    }
                    else
                    {
                        d.SetPsi(i, 0);
                    }
                }
                d.SetClosed(false);
            }
        }
        return d;
    }

    public void AddFilletRect(Rect rect, float radius)
    {
        var d = AddPath<RKCurvesDrawer>();
        var inf = RKCurves.InfinityWeight;
        var w = Mathf.Sqrt(2) / 2;
        var r2 = radius - radius/Mathf.Sqrt(2);
        d.AddControl(new ControlPoint(new Vector3(rect.xMin, rect.yMin + radius), inf));
        d.AddControl(new ControlPoint(new Vector3(rect.xMin, rect.yMax - radius), inf));

        d.AddControl(new ControlPoint(new Vector3(rect.xMin + r2, rect.yMax - r2), w));

        d.AddControl(new ControlPoint(new Vector3(rect.xMin+radius, rect.yMax), inf));
        d.AddControl(new ControlPoint(new Vector3(rect.xMax-radius, rect.yMax), inf));

        d.AddControl(new ControlPoint(new Vector3(rect.xMax - r2, rect.yMax - r2), w));

        d.AddControl(new ControlPoint(new Vector3(rect.xMax, rect.yMax - radius), inf));
        d.AddControl(new ControlPoint(new Vector3(rect.xMax, rect.yMin + radius), inf));

        d.AddControl(new ControlPoint(new Vector3(rect.xMax - r2, rect.yMin + r2), w));

        d.AddControl(new ControlPoint(new Vector3(rect.xMax - radius, rect.yMin), inf));
        d.AddControl(new ControlPoint(new Vector3(rect.xMin + radius, rect.yMin), inf));

        d.AddControl(new ControlPoint(new Vector3(rect.xMin + r2, rect.yMin + r2), w));

        d.SetClosed(true);
    }

    public T DuplicatePath<T>(MinimumPathData path, Vector3 origin)
        where T: IPathDrawer, new()
    {
        var d = AddPath<T>(path, true);
        for(int i=0; i<d.Length; i++)
        {
            d.SetPosition(i, origin + d.GetPosition(i));
        }
        return d;
    }


    private void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 240;

        buffer = new CommandBuffer();
        Cam.AddCommandBuffer(CameraEvent.AfterForwardOpaque, buffer);

        TempDrawer = new RKCurvesDrawer();
        TempDrawer.IsVisible = false;

        MethodDropdown.ClearOptions();
        MethodDropdown.AddOptions(System.Enum.GetValues(typeof(BezierCurve.CalcQMethod))
            .Cast<BezierCurve.CalcQMethod>()
            .Select(x => x.ToString())
            .ToList());
        MethodDropdown.value = (int)BezierCurve.CalcQMethod.Approximated;
        InputMgr.Run().Forget();
    }


    public (int id, int index) Moving { get; set; }
    public (int id, int index, int segment, bool sideP) Hovered { get; private set; }
    
    void Do(DrawAction action)
    {
        ActionMgr.Do(action, this).Forget();
    }

    void Update()
    {
        isRecalcMeshRequired = false;
        GlobalActionUpdate();
        var insideUI = UIArea.gameObject.activeInHierarchy && UIArea.rect.Contains(InputMgr.MousePosScreen * CanvasScaler.referenceResolution.x / Screen.width);
        if (!insideUI)
        {
            HoverUpdate();
            DrawActionUpdate();
        }
        DrawUpdate();

    }

    void GlobalActionUpdate()
    {
        if (InputMgr.GetFunction(FuncKeys.Ctrl) && Input.GetKeyDown(KeyCode.Q))
        {
            Utility.ApplicationQuit();
            return;
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            Export(false, ColorPPWSegment);
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {

#if UNITY_EDITOR
            var ssdirpath = Application.dataPath + "/Results/SS";
#else
            var ssdirpath = Application.persistentDataPath + "/Results/SS";            
#endif

            if (!System.IO.Directory.Exists(ssdirpath))
                System.IO.Directory.CreateDirectory(ssdirpath);
            else if (System.IO.File.Exists(ssdirpath + "/redirect.txt"))
                ssdirpath = System.IO.File.ReadAllText(ssdirpath + "/redirect.txt");
            ScreenCapture.CaptureScreenshot(ssdirpath + "/" + System.DateTime.Now.ToString("yyyyMMddHHmmssff") + ".png", 2);
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            
            Screen.SetResolution(
                Screen.fullScreen ? 1280 : Screen.currentResolution.width,
                Screen.fullScreen ? 720 : Screen.currentResolution.height,
                !Screen.fullScreen);
        }


        if (Input.mouseScrollDelta.y > 0)
        {
            var alpha = 1 / 1.1f;
            var prevtl = (Vector2)Cam.transform.position + new Vector2(-Cam.aspect * Cam.orthographicSize, Cam.orthographicSize);
            var mousepos = InputMgr.MousePosWorld;
            var prevt = (prevtl.y - mousepos.y);
            var prevs = (mousepos.x - prevtl.x);
            var nexttl = mousepos + new Vector3(-prevs * alpha, prevt * alpha);
            var nextc = nexttl + new Vector3(Cam.aspect * Cam.orthographicSize * alpha, -Cam.orthographicSize * alpha);
            Cam.orthographicSize *= alpha;
            Cam.transform.position = nextc + Vector3.forward * Cam.transform.position.z;
            isRecalcMeshRequired = true;
        }
        if (Input.mouseScrollDelta.y < 0)
        {
            var alpha = 1.1f;
            var prevtl = (Vector2)Cam.transform.position + new Vector2(-Cam.aspect * Cam.orthographicSize, Cam.orthographicSize);
            var mousepos = InputMgr.MousePosWorld;
            var prevt = (prevtl.y - mousepos.y);
            var prevs = (mousepos.x - prevtl.x);
            var nexttl = mousepos + new Vector3(-prevs * alpha, prevt * alpha);
            var nextc = nexttl + new Vector3(Cam.aspect * Cam.orthographicSize * alpha, -Cam.orthographicSize * alpha);
            Cam.orthographicSize *= alpha;
            Cam.transform.position = nextc + Vector3.forward * Cam.transform.position.z;
            isRecalcMeshRequired = true;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Canvas.gameObject.SetActive(!Canvas.gameObject.activeSelf);
        }

        if (InputMgr.GetSaveInput())
        {
            Save();
        }
    }
    void HoverUpdate()
    {
        Profiler.BeginSample("HoverUpdate");
        Hovered = (-1, -1, -1, false);
        var deltaMin = float.PositiveInfinity;
        foreach (var drawer in drawers.Values)
        {
            for (int i = 0; i < drawer.Length; i++)
            {
                if (drawer.Id == Moving.id && i == Moving.index)
                    continue;

                var sp = Cam.WorldToScreenPoint(drawer.GetPosition(i));
                sp.z = 0;

                var delta = (sp - InputMgr.MousePosScreen).sqrMagnitude;
                if (delta < 100)
                {
                    if (delta <= deltaMin)
                    {
                        deltaMin = delta;
                        Hovered = (drawer.Id, i, Hovered.segment, Hovered.sideP);
                    }
                }
            }
        }

        if (Hovered.id < 0)
        {
            deltaMin = float.PositiveInfinity;
            foreach (var drawer in drawers.Values)
            {
                if (drawer.IsOnPath(Cam, InputMgr.MousePosWorld, out var distance, out var segment, out var sideP, NeedShowCurvature))
                {
                    if (distance < deltaMin)
                    {
                        Hovered = (drawer.Id, -1, segment, sideP);
                        deltaMin = distance;
                    }
                }
            }
        }
        Profiler.EndSample();
    }

    void DrawActionUpdate()
    {

        Profiler.BeginSample("ActionUpdate");

        if (!ActionMgr.IsRunning)
        {
            if (Input.GetMouseButtonDown(0))
            {
                //下に何もない場合
                if (Hovered.id < 0)
                {
                    Do(CreateMode == PathType.KConics ? (DrawAction)new DAStartMakePath<RKCurvesDrawer>() : new DAStartMakePath<PPWCurveDrawer>());
                    //ActionMgr.Do(new DAMakePath(), this).Forget();
                }
                else
                {
                    //線上にある場合
                    if (Hovered.index < 0)
                    {
                        switch (InputMgr.GetFunction())
                        {
                            case FuncKeys.None:
                                Do(CreateMode == PathType.KConics ? (DrawAction)new DAStartMakePath<RKCurvesDrawer>() : new DAStartMakePath<PPWCurveDrawer>());
                                break;
                            case FuncKeys.Ctrl:
                                if(drawers[Hovered.id] is RKCurvesDrawer)
                                    Do(new DAAddCP(Hovered.id, Hovered.segment + (Hovered.sideP ? 0 : 1)));
                                else
                                    Do(new DAAddCP(Hovered.id, Hovered.segment + 1));
                                break;
                            case FuncKeys.Shift:
                                if (drawers[Hovered.id] is RKCurvesDrawer)
                                    Do(new DAChangeWeightAll(Hovered.id, 1 / Cam.orthographicSize));
                                else
                                    Do(new DAChangePhiPsi(Hovered.id, Hovered.segment, 1 / Cam.orthographicSize));
                                break;
                            case FuncKeys.Alt:
                                if(drawers[Hovered.id] is RKCurvesDrawer)
                                    Do(new DACutPath<RKCurvesDrawer>(Hovered.id, Hovered.segment - 1 + (Hovered.sideP ? 0 : 1)));
                                else
                                    Do(new DACutPath<PPWCurveDrawer>(Hovered.id, Hovered.segment));
                                break;
                            case FuncKeys.Ctrl | FuncKeys.Shift:
                                Do(new DADuplicatePath(Hovered.id));
                                break;
                            case FuncKeys.Ctrl | FuncKeys.Alt:
                                if (drawers[Hovered.id] is RKCurvesDrawer)
                                    break;
                                else
                                {
                                    var y = (PPWCurveDrawer)drawers[Hovered.id];
                                    if (y.Scheme == PPWCurve.BlendingScheme.Trigonometric)
                                        y.Scheme = PPWCurve.BlendingScheme.Hyperbolic_Extended;
                                    else
                                        y.Scheme = PPWCurve.BlendingScheme.Trigonometric;
                                }
                                break;
                            case FuncKeys.Shift | FuncKeys.Alt:
                                Do(new DAMovePath(drawers[Hovered.id]));
                                break;
                            case FuncKeys.Ctrl | FuncKeys.Shift | FuncKeys.Alt:
                                Do(new DADeletePath(drawers[Hovered.id]));
                                break;
                        }
                    }
                    //頂点上にある場合
                    else
                    {
                        switch (InputMgr.GetFunction())
                        {
                            case FuncKeys.None:
                                Do(CreateMode == PathType.KConics ? (DrawAction)new DAStartMakePath<RKCurvesDrawer>() : new DAStartMakePath<PPWCurveDrawer>());
                                break;
                            case FuncKeys.Ctrl:
                                Do(new DAMoveCP(drawers[Hovered.id], Hovered.index));
                                break;
                            case FuncKeys.Shift:
                                if (InputMgr.LClickCount == 2)
                                {
                                    Do(new DASharpenWeight(Hovered.id, Hovered.index));
                                }
                                else
                                {
                                    Do(new DAChangeWeight(Hovered.id, Hovered.index, 1/Cam.orthographicSize));
                                }
                                break;
                            case FuncKeys.Alt:
                                Do(new DADeleteCP(Hovered.id, Hovered.index));
                                break;
                            case FuncKeys.Ctrl | FuncKeys.Shift:
                                if (Hovered.index == 0 || Hovered.index == drawers[Hovered.id].Length - 1)
                                {
                                    if (drawers[Hovered.id] is RKCurvesDrawer c)
                                        Do(new DAConnectPath<RKCurvesDrawer>(Hovered.id, Hovered.index));
                                    else
                                        Do(new DAConnectPath<PPWCurveDrawer>(Hovered.id, Hovered.index));
                                }
                                else
                                {
                                    Do(new DADuplicatePath(Hovered.id));
                                }
                                break;
                            case FuncKeys.Ctrl | FuncKeys.Alt:
                                if (drawers[Hovered.id].GetCP(Hovered.index).TargetWeight < RKCurves.InfinityWeight)
                                {
                                    Do(new DASharpenWeight(Hovered.id, Hovered.index));
                                }
                                else
                                {
                                    Do(new DANormalizeWeight(Hovered.id, Hovered.index));
                                }
                                break;
                            case FuncKeys.Shift | FuncKeys.Alt:
                                Do(new DAMovePath(drawers[Hovered.id]));
                                break;
                            case FuncKeys.Ctrl | FuncKeys.Shift | FuncKeys.Alt:
                                Do(new DADeletePath(drawers[Hovered.id]));
                                break;
                        }
                    }
                }
            }
            else if (Input.GetMouseButtonDown(1))
            {
                Do(new DAMoveCanvas(Cam, 1));
            }
            else if (Input.GetMouseButtonDown(2))
            {
                Do(new DAMoveCanvas(Cam, 2));
            }

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Do(new DAClearAll());
            }


            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                var d = AddPath<RKCurvesDrawer>();
                d.AddControl(new ControlPoint(-5, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(-4.5f, 1, 0, 0.01f));
                d.AddControl(new ControlPoint(-4, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(-3, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(-2.5f, 1, 0, 0.5f));
                d.AddControl(new ControlPoint(-2, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(-1, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(-0.5f, 1));
                d.AddControl(new ControlPoint(0, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(1, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(1.5f, 1, 0, 5f));
                d.AddControl(new ControlPoint(2, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(3, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(3.5f, 1, 0, 10f));
                d.AddControl(new ControlPoint(4, 0, 0, RKCurves.InfinityWeight));

                AddFan(new Vector3(-4.5f, -1.5f), 90, 130, 0.8f,false);
                AddFan(new Vector3(-4.5f, -1.5f), 130, 90, 0.4f);

                AddFan(new Vector3(-2.5f, -1.5f), 90, 230, 0.8f,false);
                AddFan(new Vector3(-2.5f, -1.5f), 230, 90, 0.4f);

                AddFan(new Vector3(-0.5f, -1.5f), 90, 270, 0.8f,false);
                AddFan(new Vector3(-0.5f, -1.5f), 270, 90, 0.4f);

                AddFan(new Vector3(1.5f,  -1.5f), 90, 310, 0.8f, false);
                AddFan(new Vector3(1.5f, -1.5f), 310, 90, 0.4f);

                AddFan(new Vector3(3.5f,  -1.5f), 90, 400, 0.8f, false);
                AddFan(new Vector3(3.5f, -1.5f), 400, 90, 0.4f);


            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                var d = AddPath<RKCurvesDrawer>();
                d.AddControl(new ControlPoint(1, 1, 0, 0.5f));
                d.AddControl(new ControlPoint(1.866f, 1.5f, 0, 0.5f));
                d.AddControl(new ControlPoint(1.866f, 0.5f, 0, 0.5f));
                d.SetClosed(true);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                var d = AddPath<RKCurvesDrawer>();
                d.AddControl(new ControlPoint(-1, 1, 0, 1));
                d.AddControl(new ControlPoint(0,1, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(1/Mathf.Sqrt(2), 1 / Mathf.Sqrt(2), 0, 1 / Mathf.Sqrt(2)));
                d.AddControl(new ControlPoint(1, 0, 0, RKCurves.InfinityWeight));
                d.AddControl(new ControlPoint(1, -1, 0, 1));
                d.SetClosed(false);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                var whole = new Rect { size = 1.08f * new Vector2(16, 9), center = Vector2.zero };

                var mainratio = 0.8f;
                var subWidthRatio = 1 - mainratio - 0.02f;
                var textHeightRatio = 1 - mainratio - 0.02f;

                var main = new Rect(whole.xMin, whole.yMax - mainratio * whole.height, mainratio * whole.width, mainratio * whole.height);
                var sub = new Rect(whole.xMax - subWidthRatio * whole.width, whole.yMax - mainratio * whole.height, subWidthRatio * whole.width, mainratio * whole.height);
                var text = new Rect(whole.xMin, whole.yMin, whole.width, textHeightRatio * whole.height);
                //AddFilletRect(whole, 0.5f);
                AddFilletRect(main, 0.5f);
                AddFilletRect(sub, 0.5f);
                AddFilletRect(text, 0.5f);
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                var psiinf = !Input.GetKey(KeyCode.LeftShift);
                AddFanYuksel(new Vector3(0, 0), 0, 45, 1  ,psiinf);
                AddFanYuksel(new Vector3(3, 0), 0, 90, 1  ,psiinf);
                AddFanYuksel(new Vector3(6, 0), 0, 135, 1 ,psiinf);
                AddFanYuksel(new Vector3(9, 0), 0, 180, 1 ,psiinf);
                AddFanYuksel(new Vector3(0, -2), 0, 225, 1,psiinf);
                AddFanYuksel(new Vector3(3, -2), 0, 270, 1,psiinf);
                AddFanYuksel(new Vector3(6, -2), 0, 315, 1,psiinf);
                AddFanYuksel(new Vector3(9, -2), 0, 360, 1,psiinf);
            }

            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                var inf = PPWCurve.PsiInfinity;
                var prefab = new MinimumPathData
                {
                    IsClosed = false,
                    Path = new[] { new Vector3(-2, 0), new Vector3(-1, 2), new Vector3(1, -2), new Vector3(2, -1) },
                    Weights = new[]
                    {
                        1, 1, 1, 1, //w
                        1, 1, 1, 1, //phi
                        inf, 0, -inf, 0 //psi
                    }
                };
                for(int x=0; x<5; x++)
                {
                    var w = Mathf.Pow(2, x - 2);
                    for (int y=0; y<3; y++)
                    {
                        var d = DuplicatePath<PPWCurveDrawer>(prefab, new Vector3(x*5f, y*5f));
                        d.SetWeight(1, w);
                        d.SetWeight(2, w);
                        var phi = y * 3 + 1;
                        d.SetPhi(1, phi);
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                var inf = PPWCurve.PsiInfinity;
                var prefab = new MinimumPathData
                {
                    IsClosed = false,
                    Path = new[] { new Vector3(-2, 0), new Vector3(-1, 2), new Vector3(1, -2), new Vector3(2, -1) },
                    Weights = new[]
                    {
                        1, 1, 1, 1, //w
                        1, 1, 1, 1, //phi
                        inf, 0, -inf, 0 //psi
                    }
                };
                for (int x = 0; x < 5; x++)
                {
                    var psi = x == 0 ? -inf
                        : x == 4 ? inf
                        : (x - 2);
                    for (int y = 0; y < 3; y++)
                    {
                        var d = DuplicatePath<PPWCurveDrawer>(prefab, new Vector3(x * 5f, y * 5f));
                        d.SetPsi(1, psi);
                        var phi = y * 3 + 1;
                        d.SetPhi(1, phi);
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                var inf = PPWCurve.PsiInfinity;
                var r2 = Mathf.Sqrt(2); 
                var d = AddPath<PPWCurveDrawer>();
                d.AddControl(new ControlPoint(0, 0, 0, 1));
                d.AddControl(new ControlPoint(0, 1, 0, 1));
                d.AddControl(new ControlPoint(1-1/r2, 1+1/r2, 0, 1/r2));
                d.AddControl(new ControlPoint(1, 2, 0, 1));
                d.AddControl(new ControlPoint(2, 2, 0, 1));
                d.AddControl(new ControlPoint(3, 2, 0, 1));
                d.AddControl(new ControlPoint(3+1/r2, 1+1/r2, 0, 1/r2));
                d.AddControl(new ControlPoint(4, 1, 0, 1));
                d.AddControl(new ControlPoint(4, 0, 0, 1));
                for(int i=0; i<9; i++)
                {
                    d.SetPsi(i, i % 2 == 0 ? -inf : inf);
                }

                d = AddFanYuksel(new Vector3(1, 1), 45, 180, 0.5f);
                d.AddControl(new ControlPoint(0.5f, 0, 0, 1));
                d.InsertControl(0, new ControlPoint(1 + 0.5f / r2 + 1 + 0.5f / r2, 0, 0, 1));
                /*
                d = AddPath<YukselPathDrawer>();
                d.AddControl(new ControlPoint(0.5f, 0, 0, 1));
                d.AddControl(new ControlPoint(0.5f, 1, 0, 1));
                d.AddControl(new ControlPoint(1 - 0.5f / r2, 1 + 0.5f / r2, 0, 1 / r2));
                d.AddControl(new ControlPoint(1, 1.5f, 0, 1));
                d.AddControl(new ControlPoint(2, 1.5f, 0, 1));
                d.AddControl(new ControlPoint(3, 1.5f, 0, 1));
                d.AddControl(new ControlPoint(3 + 0.5f / r2, 1 + 0.5f / r2, 0, 1 / r2));
                d.AddControl(new ControlPoint(3.5f, 1, 0, 1));
                d.AddControl(new ControlPoint(3.5f, 0, 0, 1));
                */
                d.SetPsi(0, -inf);
                d.SetPsi(3, inf);
                /*
                for (int i = 0; i < 4; i++)
                {
                    d.SetPsi(i, i % 2 == 0 ? -inf : inf);
                }
                */
            }


            if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                var inf = PPWCurve.PsiInfinity;
                var r2 = Mathf.Sqrt(2);
                var d = AddPath<PPWCurveDrawer>();
                d.AddControl(new ControlPoint(0, 0, 0, 1));
                d.AddControl(new ControlPoint(0, 1, 0, 1));
                d.AddControl(new ControlPoint(1 - 1 / r2, 1 + 1 / r2, 0, 1 / r2));
                d.AddControl(new ControlPoint(1, 2, 0, 1));
                d.AddControl(new ControlPoint(2, 2, 0, 1));
                d.AddControl(new ControlPoint(3, 2, 0, 1));
                d.AddControl(new ControlPoint(3 + 1 / r2, 1 + 1 / r2, 0, 1 / r2));
                d.AddControl(new ControlPoint(4, 1, 0, 1));
                d.AddControl(new ControlPoint(4, 0, 0, 1));
            }

            if (Input.GetKeyDown(KeyCode.Alpha9))
            {
                var d = AddPath<RKCurvesDrawer>();
                d.AddControl(new ControlPoint(0, 0));
                d.AddControl(new ControlPoint(1, -1));
                d.AddControl(new ControlPoint(2.5f, 3));
                d.AddControl(new ControlPoint(3, -1));
                d.AddControl(new ControlPoint(4, 0));

                DuplicatePath<RKCurvesDrawer>(d.ToMinimumData(), new Vector3(5, 0));

                var y = AddPath<PPWCurveDrawer>();
                y.AddControl(new ControlPoint(10+0, 0));
                y.AddControl(new ControlPoint(10+1, -1));
                y.AddControl(new ControlPoint(10+2.5f, 3));
                y.AddControl(new ControlPoint(10+3, -1));
                y.AddControl(new ControlPoint(10+4, 0));
                y.Scheme = PPWCurve.BlendingScheme.Trigonometric;

                y = DuplicatePath<PPWCurveDrawer>(y.ToMinimumData(), new Vector3(5,0));
                y.Scheme = PPWCurve.BlendingScheme.Hyperbolic_Extended;
                y = DuplicatePath<PPWCurveDrawer>(y.ToMinimumData(), new Vector3(5, 0));
                y.Scheme = PPWCurve.BlendingScheme.Hyperbolic_Extended;
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                var y = AddPath<PPWCurveDrawer>();
                y.AddControl(new ControlPoint(0, 0));
                y.AddControl(new ControlPoint(1, 1));
                y.AddControl(new ControlPoint(2, 0));
                y.AddControl(new ControlPoint(0, -2));
                y.AddControl(new ControlPoint(2, -2));
                y.AddControl(new ControlPoint(2.1f, -1.8f));
                y.SetClosed(false);
                y.Scheme = PPWCurve.BlendingScheme.Trigonometric;

                y = DuplicatePath<PPWCurveDrawer>(y.ToMinimumData(), new Vector3(5, 0));
                y.Scheme = PPWCurve.BlendingScheme.Trigonometric;
                y.SetWeight(0, 1);
                y.SetWeight(1, 0.5f);
                y.SetWeight(2, 0.75f);
                y.SetWeight(3, 10);
                y.SetWeight(4, 4);
                y.SetWeight(5, 1);

                y = DuplicatePath<PPWCurveDrawer>(y.ToMinimumData(), new Vector3(5, 0));
                y.Scheme = PPWCurve.BlendingScheme.Hyperbolic_Extended;
                y.SetPhi(0, 1f);
                y.SetPhi(1, 1f);
                y.SetPhi(2, 10f);
                y.SetPhi(3, 1f);
                y.SetPhi(4, 1f);
                y.SetPsi(0, PPWCurve.PsiInfinity);
                y.SetPsi(1, -1.3f);
                y.SetPsi(2, -0.3f);
                y.SetPsi(3, 0.5f);
                y.SetPsi(4, -PPWCurve.PsiInfinity);
            }

            if (InputMgr.GetUndoInput())
            {
                ActionMgr.Undo(this);
            }
            else if (InputMgr.GetRedoInput())
            {
                ActionMgr.Redo(this);
            }
        }



        Profiler.EndSample();

    }



    void DrawUpdate()
    {
        Profiler.BeginSample("DrawUpdate");
        buffer.Clear();
        foreach (var p in drawers.Values)
        {
            var option = new ConicPathDrawerOption
            {
                NeedRecalcMesh = isRecalcMeshRequired,
                NeedBezierCP = NeedShowPolygon,
                NeedPolygon = NeedShowPolygon,
                NeedCP = NeedShowCPDefault || InputMgr.GetFunction() != FuncKeys.None,
                NeedPathColorChangeOnHover = InputMgr.GetFunction() != FuncKeys.None,
                NeedCPColorChangeOnHover = InputMgr.GetFunction() != FuncKeys.None,
                NeedWeightText = NeedShowWeight,
                NeedCurvature = NeedShowCurvature,
                IsHovered = Hovered.id == p.Id,
                HoveredCP = Hovered.index,
                HoveredSegment = Hovered.segment,
                IsHoveredAtSideP = Hovered.sideP,
            };
            p.Render(Cam, buffer, LineMaterial, option);
        }
        TempDrawer.Render(Cam, buffer, LineMaterial, new ConicPathDrawerOption
        {
            NeedRecalcMesh = isRecalcMeshRequired,
            NeedBezierCP = false,
            NeedPolygon = false,
            NeedCP = false,
            NeedPathColorChangeOnHover = true,
            NeedCPColorChangeOnHover = false,
            NeedWeightText = false,
            NeedCurvature = NeedShowCurvature,
            IsHovered = true,
            HoveredCP = -1,
            HoveredSegment = 0,
            IsHoveredAtSideP = false,
        });
        isRecalcMeshRequired = false;
        Profiler.EndSample();
    }

    public void Save()
    {
#if UNITY_EDITOR
        var dir = $"{Application.dataPath}/Results/JSON";
#else
        var dir = $"{Application.persistentDataPath}/Results/JSON";
#endif


        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var path = $"{dir}/{FileName}.json";

        if (File.Exists(path))
        {
            File.Copy(path, path + ".backup", true);
        }
        var json = Utf8Json.JsonSerializer.ToJsonString(new MinimumCanvasData(drawers.Values));
        json = Utility.ToReadableJson(json);
        File.WriteAllText(path, json);
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    public void Load()
    {
        drawers.Clear();
#if UNITY_EDITOR
        var dir = $"{Application.dataPath}/Results/JSON";
#else
        var dir = $"{Application.persistentDataPath}/Results/JSON";
#endif

        var path = $"{dir}/{FileName}.json";
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var ds = Utf8Json.JsonSerializer.Deserialize<MinimumCanvasData>(json);
            foreach (var p in ds.Paths)
            {
                AddPath(p);
                maxid = Mathf.Max(p.Id, maxid);
            }
        }
    }

    public void Export(bool darkmode = false, bool colorYukselSegment = false)
    {
        var data = new StNoh.SvgData();
        var lines = data.Lines;
        var points = data.Points;

        //Control polygon
        if (NeedShowPolygon)
        {
            foreach (var d in drawers.Values)
            {
                if(d is RKCurvesDrawer cd)
                {
                    var plot = cd.GetPolygonPlotData().ToList();
                    if (d.IsClosed && d.Length >= 3)
                    {
                        plot.Add(plot[0]);
                    }
                    lines.Add((plot
                        .Select(x => Cam.WorldToScreenPoint(x))
                        .Select(x => new Vector2(x.x, Screen.height - x.y))
                        .ToList(), new Color(1, 0, 1, 0.3f)));

                    if (d.IsClosed && d.Length >= 3)
                    {
                        plot.Add(plot[0]);
                    }
                    points.AddRange(cd.GetPolygonPlotData()
                        .Select(x => Cam.WorldToScreenPoint(x))
                        .Select(x => (new Vector2(x.x, Screen.height - x.y), Color.white)));
                }
                else if (d is PPWCurveDrawer yd)
                {
                    foreach (var plot in yd.GetTmpPlotData())
                    {
                        lines.Add((plot
                            .Select(x => Cam.WorldToScreenPoint(x))
                            .Select(x => new Vector2(x.x, Screen.height - x.y))
                            .ToList(), new Color(0, 0, 0, 0.1f)));
                    }
                }
            }
        }

        //Curvature
        if (NeedShowCurvature)
        {
            foreach (var d in drawers.Values)
            {
                lines.AddRange(d.GetCurvaturePlotData()
                    .Select(x => (from: Cam.WorldToScreenPoint(x.from), to: Cam.WorldToScreenPoint(x.to)))
                    .Select(x => (new List<Vector2>()
                    {
                        new Vector2(x.from.x, Screen.height - x.from.y),
                        new Vector2(x.to.x, Screen.height-x.to.y)
                    },
                    new Color(0.5f, 0, 0.5f, 0.2f))));
            }
        }


        //Path
        foreach (var d in drawers.Values)
        {
            if(d is RKCurvesDrawer || !colorYukselSegment || ((PPWCurveDrawer) d).Scheme != PPWCurve.BlendingScheme.Hyperbolic_Extended)
            {
                lines.Add((d.GetPlotData()
                    .Select(x => Cam.WorldToScreenPoint(x))
                    .Select(x => new Vector2(x.x, Screen.height - x.y))
                    .ToList(), darkmode ? Color.white : Color.black));
            }
            else if (d is PPWCurveDrawer y)
            {
                var plots = d.GetPlotData()
                    .Select(x => Cam.WorldToScreenPoint(x))
                    .Select(x => new Vector2(x.x, Screen.height - x.y));
                for (int i=0; i<(d.IsClosed ? d.Length : d.Length-1); i++)
                {
                    var segment = plots.Skip(i * d.PlotStepPerSegment).Take(d.PlotStepPerSegment + 1);
                    lines.Add((segment.ToList(), PsiPhiColor3(y.GetPsi(i), y.GetPhi(i))));
                    
                    /*
                    lines.Add((segment.ToList(), ColorUtil.HSV(
                        0.5f + (1 + Mathf.Clamp(y.GetPsi(i), -PPWCurve.PsiInfinity, PPWCurve.PsiInfinity) / PPWCurve.PsiInfinity)/4,
                        1, 
                        0.5f + Mathf.Clamp(y.GetPhi(i), 0, 8)/16)));
                    */
                }
            }
        }

        //Control points
        if (NeedShowCPDefault)
        {
            foreach (var d in drawers.Values)
            {
                for (int i = 0; i < d.Length; i++)
                {
                    var cp = d.GetCP(i);
                    Color cpcol = ColorUtil.Jet(Mathf.Log10(cp.TargetWeight) / 3 + 0.5f);
                    var cppos = Cam.WorldToScreenPoint(cp.Position);
                    cppos.y = Screen.height - cppos.y;
                    points.Add((cppos, cpcol));
                }
            }
        }

#if UNITY_EDITOR
        var dir = $"{Application.dataPath}/Results/SVG";
#else
        var dir = $"{Application.persistentDataPath}/Results/SVG";
#endif

        StNoh.SvgExporter.WriteSVG(
            filepath:   $"{dir}/{System.DateTime.Now.ToString("MMddHHmmssff")}_{FileName}.svg",
            canvas:     new Vector2Int(Screen.width, Screen.height), 
            data:       data,
            darkmode:   darkmode);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

    }

    public static Color PsiPhiColor(float psi, float phi)
    {
        var phinormal = Mathf.Clamp(phi,0,6) / 6;
        var dark = 0.4f + phinormal * 0.6f;
        var jet = ColorUtil.Jet(psi, -PPWCurve.PsiInfinity/2, PPWCurve.PsiInfinity/2);
        return new Color(jet.r * dark, jet.g * dark, jet.b * dark);
    }


    public static Color PsiPhiColor2(float psi, float phi)
    {
        var phinormal = Mathf.Clamp(phi, 0, 6) / 6;
        var psinormal = Mathf.Clamp(psi, -PPWCurve.PsiInfinity / 2, PPWCurve.PsiInfinity / 2) / PPWCurve.PsiInfinity + 0.5f;
        return new Color(psinormal, phinormal, 0);
    }


    public static Color PsiPhiColor3(float psi, float phi)
    {
        var phinormal = Mathf.Clamp(phi, 0, 6) / 6;
        var psinormal = Mathf.Clamp(psi, -PPWCurve.PsiInfinity, PPWCurve.PsiInfinity) / PPWCurve.PsiInfinity;
        if (psi < 0)
            return new Color(0, phinormal, Mathf.Abs(psinormal));
        else
            return new Color(Mathf.Abs(psinormal), phinormal, 0);
    }
}
