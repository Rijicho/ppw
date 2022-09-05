using UnityEngine;
using RUtil.Curve;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;


public abstract class DrawAction
{
    public abstract string Name { get; }
    public abstract UniTask<bool> Do(DrawingCanvas e);
    public abstract void Undo(DrawingCanvas e);
    public abstract void Redo(DrawingCanvas e);

    public virtual void OnFinish(DrawingCanvas e) { }
}

public class DAStartMakePath<T> : DrawAction
    where T: IPathDrawer, new()
{
    public override string Name => "MkP";

    MinimumPathData created;

    public override  UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.AddPath<T>();
        var p0 = InputMgr.MousePosWorld;

        var func = InputMgr.GetFunction();

        var w = func == FuncKeys.Ctrl ? RKCurves.InfinityWeight : func == FuncKeys.Alt ? 0.5f : 1;

        drawer.AddControl(new ControlPoint(p0, w));
        
        if(drawer is PPWCurveDrawer yd)
        {
            yd.SetPsi(0, PPWCurve.PsiInfinity);
        }

        created = drawer.ToMinimumData();
        return UniTask.FromResult(true);
    }

    public override void OnFinish(DrawingCanvas e)
    {
        ActionMgr.Do(new DAAddCPAtLast<T>(created.Id), e).Forget();
    }


    public override  void Redo(DrawingCanvas e)
    {
        e.AddPath<T>(created);
        ActionMgr.Do(new DAAddCPAtLast<T>(created.Id), e).Forget();
    }

    public override void Undo(DrawingCanvas e)
    {
        e.RemovePath(created.Id);
    }
}

public class DAAddCPAtLast<T> : DrawAction
    where T : IPathDrawer, new()
{
    public override string Name => "ACPL";
    int id;
    float weight;
    ControlPoint p;

    MinimumPathData connectFromData;
    MinimumPathData connectToData;
    MinimumPathData connectResultData;

    enum FinishState
    {
        Submit,
        Next,
        Close,
        Connect,
        Undo,
        Redo,
    }
    FinishState state;

    bool isLast;

    int connectTo;
    bool isConnectToEnd;

    public DAAddCPAtLast(int id)
    {
        this.id = id;
        connectTo = -1;        
    }

    public override async UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(id);
        drawer.AddControl(new ControlPoint(InputMgr.MousePosWorld));
        if (drawer.Length > 2 && drawer is PPWCurveDrawer yd)
        {
            yd.SetPsi(drawer.Length-2, -PPWCurve.PsiInfinity);
        }

        e.Moving = (id, drawer.Length - 1);
        await UniTask.Yield();

        while (true)
        {
            var mousepos = InputMgr.MousePosWorld;
            if (drawer.GetPosition(-1) != mousepos)
            {
                drawer.SetPosition(-1, mousepos);
            }

            var func = InputMgr.GetFunction();

            bool closing = func == (FuncKeys.Shift)
                && drawer.Length > 3 
                && e.Hovered.id == id 
                && e.Hovered.index == 0;

            var hoveredDrawer = e.Hovered.id >= 0 ? e.GetDrawer(e.Hovered.id) : default;

            bool connecting = func == (FuncKeys.Shift) 
                && hoveredDrawer != null
                && e.Hovered.id != id 
                && !hoveredDrawer.IsClosed
                && (e.Hovered.index == 0 || e.Hovered.index == hoveredDrawer.Length - 1);

            drawer.SetClosed(closing);
            if (closing)
            {
                drawer.IsVisible = false;
                while (e.TempDrawer.Length < drawer.Length-1)
                {
                    e.TempDrawer.AddControl(Vector3.zero);
                }
                while (e.TempDrawer.Length > drawer.Length-1)
                {
                    e.TempDrawer.RemoveLastControl();
                }
                for (int i = 0; i < drawer.Length - 1; i++)
                {
                    e.TempDrawer.SetPosition(i, drawer.GetPosition(i));
                    e.TempDrawer.SetWeight(i, drawer.GetWeight(i));
                }
                e.TempDrawer.SetClosed(true);
                e.TempDrawer.IsVisible = true;
            }
            else
            {
                drawer.IsVisible = true;
                e.TempDrawer.IsVisible = false;
            }

            if (connecting)
            {
                connectTo = e.Hovered.id;
                isConnectToEnd = e.Hovered.index != 0;

                drawer.IsVisible = false;
                hoveredDrawer.IsVisible = false;

                while(e.TempDrawer.Length < drawer.Length + hoveredDrawer.Length - 1)
                {
                    e.TempDrawer.AddControl(Vector3.zero);
                }
                while(e.TempDrawer.Length > drawer.Length + hoveredDrawer.Length - 1)
                {
                    e.TempDrawer.RemoveLastControl();
                }
                for(int i=0; i<drawer.Length-1; i++)
                {
                    e.TempDrawer.SetPosition(i, drawer.GetPosition(i));
                    e.TempDrawer.SetWeight(i, drawer.GetWeight(i));
                }
                if (isConnectToEnd)
                {
                    for (int i = 0; i < hoveredDrawer.Length; i++)
                    {
                        e.TempDrawer.SetPosition(drawer.Length + i - 1, hoveredDrawer.GetPosition(hoveredDrawer.Length - i - 1));
                        e.TempDrawer.SetWeight(drawer.Length + i - 1, hoveredDrawer.GetWeight(hoveredDrawer.Length - i - 1));
                    }
                }
                else
                {
                    for (int i = 0; i < hoveredDrawer.Length; i++)
                    {
                        e.TempDrawer.SetPosition(drawer.Length + i - 1, hoveredDrawer.GetPosition(i));
                        e.TempDrawer.SetWeight(drawer.Length + i - 1, hoveredDrawer.GetWeight(i));
                    }
                }
                e.TempDrawer.SetClosed(false);
                e.TempDrawer.IsVisible = true;
            }
            else if (connectTo != -1)
            {
                drawer.IsVisible = true;
                e.GetDrawer(connectTo).IsVisible = true;
                e.TempDrawer.IsVisible = false;
                connectTo = -1;
            }


            if (Input.GetMouseButtonDown(0))
            {
                if (ActionMgr.PrevAction is DAAddCPAtLast<T> pa && InputMgr.LClickCount == 2)
                {
                    drawer.SetClosed(false);
                    drawer.RemoveLastControl();
                    if (drawer.Length > 2 && drawer is PPWCurveDrawer y)
                    {
                        y.SetPsi(drawer.Length - 2, -PPWCurve.PsiInfinity);
                    }
                    state = FinishState.Submit;
                    pa.isLast = true;
                    return false; //終了
                }
                else
                {
                    if(closing)
                    {
                        drawer.RemoveLastControl();
                        drawer.SetClosed(true);
                        drawer.IsVisible = true;
                        state = FinishState.Close;
                        return true;
                    }
                    else if (connecting)
                    {
                        drawer.RemoveLastControl();
                        connectFromData = drawer.ToMinimumData();
                        connectToData = e.GetDrawer(connectTo).ToMinimumData();

                        e.RemovePath(drawer.Id);
                        e.RemovePath(connectTo);
                        var newpathData = e.TempDrawer.ToMinimumData();
                        System.Array.Resize(ref newpathData.Weights, newpathData.Weights.Length * 2);
                        var newpath = e.AddPath<T>(newpathData, true);
                        connectResultData = newpath.ToMinimumData();
                        state = FinishState.Connect;
                        return true; //他のパスに接続
                    }
                    else
                    {
                        weight = func == FuncKeys.Ctrl ? RKCurves.InfinityWeight : func == FuncKeys.Alt ? 0.5f : 1;
                        drawer.SetWeight(drawer.Length - 1, weight);
                        if (drawer.Length > 2 && drawer is PPWCurveDrawer y)
                        {
                            y.SetPsi(drawer.Length-2, 0);
                        }

                        p = drawer.GetCP(drawer.Length - 1);
                        state = FinishState.Next;
                        return true; //次の制御点追加を開始
                    }
                }
            }
            else if (Input.GetMouseButtonDown(1) || InputMgr.GetUndoInput())
            {
                drawer.SetClosed(false);
                drawer.RemoveLastControl();
                state = FinishState.Undo;
                return false; //Undo
            }
            else if (!ActionMgr.IsOnTop && InputMgr.GetRedoInput())
            {
                drawer.SetClosed(false);
                drawer.RemoveLastControl();
                state = FinishState.Redo;
                return false; //Redo
            }
            await UniTask.Yield();
        }
    }

    public override void OnFinish(DrawingCanvas e)
    {
        e.Moving = (-1, -1);
        e.TempDrawer.IsVisible = false;
        switch (state)
        {
            case FinishState.Next:
                ActionMgr.Do(new DAAddCPAtLast<T>(id), e).Forget();
                break;
            case FinishState.Submit:
                break;
            case FinishState.Close:
                break;
            case FinishState.Connect:
                break;
            case FinishState.Undo:
                ActionMgr.Undo(e);
                break;
            case FinishState.Redo:
                ActionMgr.Redo(e);
                break;
        }
    }

    public override void Redo(DrawingCanvas e)
    {
        switch (state)
        {
            case FinishState.Next:
                e.GetDrawer(id).AddControl(p);
                if(!isLast)
                    ActionMgr.Do(new DAAddCPAtLast<T>(id), e).Forget();
                break;
            case FinishState.Close:
                e.GetDrawer(id).SetClosed(true);
                break;
            case FinishState.Connect:
                e.RemovePath(connectFromData.Id);
                e.RemovePath(connectToData.Id);
                e.AddPath<T>(connectResultData);
                break;
            default:
                Debug.LogError("予期しない挙動です");
                break;
        }
    }

    public override void Undo(DrawingCanvas e)
    {
        e.Moving = (-1, -1);
        e.TempDrawer.IsVisible = false;
        switch (state)
        {
            case FinishState.Next:
                e.GetDrawer(id).RemoveLastControl();
                ActionMgr.Do(new DAAddCPAtLast<T>(id), e).Forget();
                break;
            case FinishState.Close:
                e.GetDrawer(id).SetClosed(false);
                ActionMgr.Do(new DAAddCPAtLast<T>(id), e).Forget();
                break;
            case FinishState.Connect:
                e.RemovePath(connectResultData.Id);
                e.AddPath<T>(connectFromData);
                e.AddPath<T>(connectToData);
                ActionMgr.Do(new DAAddCPAtLast<T>(id), e).Forget();
                break;
            default:
                Debug.LogError("予期しない挙動です");
                break;
        }
    }
}

public class DAMoveCP : DrawAction
{
    public override string Name => "MvCP";
    int id;
    int index;
    Vector3 prev;
    Vector3 next;

    public DAMoveCP(IPathDrawer drawer, int pointIndex)
    {
        id = drawer.Id;
        index = pointIndex;
        prev = drawer.GetPosition(index);
    }

    public override async UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(id);
        while (Input.GetMouseButton(0))
        {
            drawer.SetPosition(index, InputMgr.MousePosWorld);
            await UniTask.Yield();
        }
        next = drawer.GetPosition(index);
        return prev != next;
    }

    public override void Redo(DrawingCanvas e)
    {
        e.GetDrawer(id).SetPosition(index, next);
    }

    public override void Undo(DrawingCanvas e)
    {
        e.GetDrawer(id).SetPosition(index, prev);
    }
}

public class DAAddCP : DrawAction
{
    public override string Name => "ACP";

    int id;
    int index;
    ControlPoint cp;

    public DAAddCP(int id, int index)
    {
        this.id = id;
        this.index = index;
    }

    public override async UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(id);
        drawer.InsertControl(index, new ControlPoint(InputMgr.MousePosWorld));

        while (Input.GetMouseButton(0))
        {
            drawer.SetPosition(index, InputMgr.MousePosWorld);
            await UniTask.Yield();
        }
        cp = drawer.GetCP(index);
        return true;
    }

    public override void Redo(DrawingCanvas e)
    {
        e.GetDrawer(id).InsertControl(index, cp);
    }

    public override void Undo(DrawingCanvas e)
    {
        e.GetDrawer(id).RemoveControlAt(index);
    }
}

public class DAChangeWeight : DrawAction
{
    public override string Name => "CW";

    int id;
    int index;
    float prev;
    float next;
    float scale;

    public DAChangeWeight(int id, int index, float scale)
    {
        this.id = id;
        this.index = index;
        this.scale = scale;
    }

    public override async UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(id);
        prev = drawer.GetWeight(index);
        var defaultPos = InputMgr.MousePosWorld;

        while (Input.GetMouseButton(0))
        {
            var currentPos = InputMgr.MousePosWorld;

            drawer.SetWeight(index, Mathf.Max(0.01f, prev + (currentPos.x - defaultPos.x) * scale));

            await UniTask.Yield();
        }
        next = drawer.GetWeight(index);
        return prev != next;
    }

    public override void Redo(DrawingCanvas e)
    {
        e.GetDrawer(id).SetWeight(index, next);
    }

    public override void Undo(DrawingCanvas e)
    {
        e.GetDrawer(id).SetWeight(index, prev);
    }
}



public class DAChangePhiPsi : DrawAction
{
    public override string Name => "CW";

    int id;
    int index;
    float prevphi;
    float nextphi;
    float prevpsi;
    float nextpsi;
    float scale;

    public DAChangePhiPsi(int id, int index, float scale)
    {
        this.id = id;
        this.index = index;
        this.scale = scale;
    }

    public override async UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer<PPWCurveDrawer>(id);
        prevphi = drawer.GetPhi(index);
        prevpsi = drawer.GetPsi(index);
        var defaultPos = InputMgr.MousePosWorld;

        Vector3 dir = drawer.GetCP(index + 1) - drawer.GetCP(index);
        dir.Normalize();
        Vector3 ndir = dir.x >= 0 ? Quaternion.Euler(0, 0, 90) * dir : Quaternion.Euler(0, 0, -90) * dir;

        while (Input.GetMouseButton(0))
        {
            var currentPos = InputMgr.MousePosWorld;

            drawer.SetPhi(index, Mathf.Clamp(prevphi + Vector3.Dot(currentPos - defaultPos, ndir) * scale*10, 0.01f, 10f));

            var newpsi = prevpsi + Vector3.Dot(currentPos - defaultPos, dir) * scale * 3;

            if(Mathf.Abs(newpsi) < PPWCurve.PsiInfinity)
            {
                drawer.SetPsi(index, Mathf.Clamp(newpsi, -PPWCurve.PsiInfinity, PPWCurve.PsiInfinity));
            }
            else if (newpsi < 0)
            {
                drawer.SetPsi(index, -PPWCurve.PsiInfinity);
            }
            else
            {
                drawer.SetPsi(index, PPWCurve.PsiInfinity);
            }


            await UniTask.Yield();
        }
        nextphi = drawer.GetPhi(index);
        nextpsi = drawer.GetPsi(index);
        return prevphi != nextphi || prevpsi != nextpsi;
    }

    public override void Redo(DrawingCanvas e)
    {
        var drawer = e.GetDrawer<PPWCurveDrawer>(id);
        drawer.SetPhi(index, nextphi);
        drawer.SetPsi(index, nextpsi);
    }

    public override void Undo(DrawingCanvas e)
    {
        var drawer = e.GetDrawer<PPWCurveDrawer>(id);
        drawer.SetPhi(index, prevphi);
        drawer.SetPsi(index, prevpsi);
    }
}



public class DAChangeWeightAll : DrawAction
{
    public override string Name => "CWA";

    int id;

    MinimumPathData prev;
    MinimumPathData next;

    float scale;

    public DAChangeWeightAll(int id, float scale)
    {
        this.id = id;
        this.scale = scale;
    }

    public override async UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(id);
        prev = drawer.ToMinimumData();
        var defaultPos = InputMgr.MousePosWorld;

        while (Input.GetMouseButton(0))
        {
            var currentPos = InputMgr.MousePosWorld;

            for(int i=0; i<drawer.Length; i++)
            {
                drawer.SetWeight(i, Mathf.Max(0.01f, prev.Weights[i] + (currentPos.x - defaultPos.x) * scale));
            }
            await UniTask.Yield();
        }
        next = drawer.ToMinimumData();
        return defaultPos != InputMgr.MousePosWorld;
    }

    public override void Redo(DrawingCanvas e)
    {
        var d = e.GetDrawer(id);
        for(int i=0; i<d.Length; i++)
        {
            d.SetWeight(i, next.Weights[i]);
        }
    }

    public override void Undo(DrawingCanvas e)
    {
        var d = e.GetDrawer(id);
        for (int i = 0; i < d.Length; i++)
        {
            d.SetWeight(i, prev.Weights[i]);
        }
    }
}

public class DASharpenWeight : DrawAction
{
    public override string Name => "SW";

    int id;
    int index;
    float prev;

    public DASharpenWeight(int id, int index)
    {
        this.id = id;
        this.index = index;
    }

    public override UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(id);
        prev = drawer.GetWeight(index);
        drawer.SetWeight(index, RKCurves.InfinityWeight);
        return UniTask.FromResult(true);
    }

    public override void Redo(DrawingCanvas e)
    {
        e.GetDrawer(id).SetWeight(index, RKCurves.InfinityWeight);
    }

    public override void Undo(DrawingCanvas e)
    {
        e.GetDrawer(id).SetWeight(index, prev);
    }
}


public class DANormalizeWeight : DrawAction
{
    public override string Name => "NW";

    int id;
    int index;
    float prev;

    public DANormalizeWeight(int id, int index)
    {
        this.id = id;
        this.index = index;
    }

    public override UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(id);
        prev = drawer.GetWeight(index);
        drawer.SetWeight(index, 1);
        return UniTask.FromResult(true);
    }

    public override void Redo(DrawingCanvas e)
    {
        e.GetDrawer(id).SetWeight(index, 1);
    }

    public override void Undo(DrawingCanvas e)
    {
        e.GetDrawer(id).SetWeight(index, prev);
    }
}

public class DADeletePath : DrawAction
{
    public override string Name => "DP";

    MinimumPathData prev;

    public DADeletePath(IPathDrawer d)
    {
        prev = d.ToMinimumData();
    }

    public override UniTask<bool> Do(DrawingCanvas e)
    {
        e.RemovePath(prev.Id);
        return UniTask.FromResult(true);
    }

    public override void Redo(DrawingCanvas e)
    {
        e.RemovePath(prev.Id);
    }

    public override void Undo(DrawingCanvas e)
    {
        e.AddPath(prev);
    }
}

public class DADeleteCP : DrawAction
{
    public override string Name => "DCP";

    int id;
    int index;

    MinimumPathData prev;
    MinimumPathData next;

    public DADeleteCP(int id, int index)
    {
        this.id = id;
        this.index = index;
    }

    public override UniTask<bool> Do(DrawingCanvas e)
    {
        var d = e.GetDrawer(id);
        prev = d.ToMinimumData();

        if(prev.Path.Length > 2)
        {
            d.RemoveControlAt(index);
            next = d.ToMinimumData();
        }
        else
        {
            e.RemovePath(prev.Id);
            next = null;
        }
        return UniTask.FromResult(true);
    }

    public override void Redo(DrawingCanvas e)
    {
        e.RemovePath(prev.Id);
        if (next != null)
            e.AddPath(next);
    }

    public override void Undo(DrawingCanvas e)
    {
        if (next != null)
            e.RemovePath(next.Id);
        e.AddPath(prev);
    }
}

public class DAMovePath : DrawAction
{
    public override string Name => "MP";

    MinimumPathData prev;
    MinimumPathData next;

    public DAMovePath(IPathDrawer drawer)
    {
        prev = drawer.ToMinimumData();
    }

    public override async UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(prev.Id);
        var defaultpos = InputMgr.MousePosWorld;
        while (Input.GetMouseButton(0))
        {
            await UniTask.Yield();
            for (int i=0; i<drawer.Length; i++)
            {
                drawer.SetPosition(i, prev.Path[i] + InputMgr.MousePosWorld - defaultpos);
            }
        }
        next = drawer.ToMinimumData();
        return defaultpos != InputMgr.MousePosWorld;
    }

    public override void Redo(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(prev.Id);
        for (int i = 0; i < drawer.Length; i++)
        {
            drawer.SetPosition(i, next.Path[i]);
        }
    }

    public override void Undo(DrawingCanvas e)
    {

        var drawer = e.GetDrawer(prev.Id);
        for (int i = 0; i < drawer.Length; i++)
        {
            drawer.SetPosition(i, prev.Path[i]);
        }
    }
}

public class DACutPath<T> : DrawAction
    where T: IPathDrawer, new()
{
    public override string Name => "CtP";

    int id;
    int index;
    MinimumPathData prev;
    MinimumPathData next0;
    MinimumPathData next1;

    //cp[index]→cp[index+1]の部分を削除
    public DACutPath(int id, int index)
    {
        this.id = id;
        this.index = index;
    }

    public override UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(id);
        prev = drawer.ToMinimumData();

        if (prev.IsClosed)
        {
            var next0drawer = e.AddPath<T>();

            for(int i=0; i<prev.Path.Length; i++)
            {
                next0drawer.AddControl(drawer.GetCP((index + i + 1) % drawer.Length));
            }
            next0 = next0drawer.ToMinimumData();
            next1 = null;
            e.RemovePath(prev.Id);
        }
        else if (prev.Path.Length == 2)
        {
            next0 = next1 = null;
            e.RemovePath(prev.Id);
        }
        else if (index == 0)
        {
            var next0drawer = e.AddPath<T>();

            for (int i = 1; i < prev.Path.Length; i++)
            {
                next0drawer.AddControl(drawer.GetCP(i));
            }
            next0 = next0drawer.ToMinimumData();
            next1 = null;
            e.RemovePath(prev.Id);

        }
        else if (index == prev.Path.Length - 2)
        {
            var next0drawer = e.AddPath<T>();

            for (int i = 0; i < prev.Path.Length-1; i++)
            {
                next0drawer.AddControl(drawer.GetCP(i));
            }
            next0 = next0drawer.ToMinimumData();
            next1 = null;
            e.RemovePath(prev.Id);
        }
        else
        {
            var next0drawer = e.AddPath<T>();
            var next1drawer = e.AddPath<T>();

            for (int i = 0; i <= index; i++)
            {
                next0drawer.AddControl(drawer.GetCP(i));
            }

            for(int i = index + 1; i < prev.Path.Length; i++)
            {
                next1drawer.AddControl(drawer.GetCP(i));
            }
            next0 = next0drawer.ToMinimumData();
            next1 = next1drawer.ToMinimumData();

            e.RemovePath(prev.Id);
        }

        return UniTask.FromResult(true);
    }

    public override void Redo(DrawingCanvas e)
    {
        e.RemovePath(prev.Id);
        if( next0 != null)
            e.AddPath<T>(next0);
        if (next1!=null)
            e.AddPath<T>(next1);
    }

    public override void Undo(DrawingCanvas e)
    {
        if(next0 != null)
            e.RemovePath(next0.Id);
        if (next1 != null)
            e.RemovePath(next1.Id);
        e.AddPath<T>(prev);
    }
}

public class DADuplicatePath : DrawAction
{
    public override string Name => "DpP";

    int id;
    MinimumPathData next;

    public DADuplicatePath(int id)
    {
        this.id = id;
    }

    public override async UniTask<bool> Do(DrawingCanvas e)
    {
        var src = e.GetDrawer(id);
        var srcdata = src.ToMinimumData();
        var drawer = e.AddPath(srcdata, true);
        var defaultpos = InputMgr.MousePosWorld;
        while (Input.GetMouseButton(0))
        {
            await UniTask.Yield();
            for (int i = 0; i < drawer.Length; i++)
            {
                drawer.SetPosition(i, srcdata.Path[i] + InputMgr.MousePosWorld - defaultpos);
            }
        }
        next = drawer.ToMinimumData();

        if(defaultpos != InputMgr.MousePosWorld)
        {
            return true;
        }
        else
        {
            e.RemovePath(drawer.Id);
            return false;
        }

    }

    public override void Redo(DrawingCanvas e)
    {
        e.AddPath(next);
    }

    public override void Undo(DrawingCanvas e)
    {
        e.RemovePath(next.Id);
    }
}


public class DAConnectPath<T> : DrawAction
    where T: IPathDrawer, new()
{
    public override string Name => "CnP";

    int id;
    int index;
    bool from0;

    bool closing;
    MinimumPathData prev0;
    MinimumPathData prev1;
    MinimumPathData next;

    public DAConnectPath(int id, int index)
    {
        this.id = id;
        this.index = index;
        this.from0 = index == 0;
    }

    public override async UniTask<bool> Do(DrawingCanvas e)
    {
        var drawer = e.GetDrawer(id);
        prev0 = drawer.ToMinimumData();

        e.TempDrawer.IsVisible = true;
        e.TempDrawer.Clear();
        e.TempDrawer.AddControl(drawer.GetCP(from0 ? 0 : drawer.Length-1));
        e.TempDrawer.AddControl(InputMgr.MousePosWorld);

        closing = false;
        bool connecting = false;

        while (Input.GetMouseButton(0))
        {
            await UniTask.Yield();



            closing = connecting = false;
            if(e.Hovered.id >= 0 && e.Hovered.index >= 0)
            {
                var target = e.GetDrawer(e.Hovered.id);
                if(e.Hovered.index == 0 || e.Hovered.index == target.Length - 1)
                {
                    if(e.Hovered.id == id && e.Hovered.index != index)
                    {
                        closing = true;
                        connecting = false;
                    }
                    else
                    {
                        closing = false;
                        connecting = true;
                    }
                }
            }
            if (closing)
            {
                e.TempDrawer.SetPosition(1, drawer.GetPosition(e.Hovered.index));
            }
            else if (connecting)
            {
                e.TempDrawer.SetPosition(1, e.GetDrawer(e.Hovered.id).GetPosition(e.Hovered.index));
            }
            else
            {
                e.TempDrawer.SetPosition(1, InputMgr.MousePosWorld);
            }
        }

        if (closing)
        {
            drawer.SetClosed(true);
            return true;
        }
        if (connecting)
        {
            var target = e.GetDrawer(e.Hovered.id);
            var nextdrawer = e.AddPath<T>();
            if (from0)
            {
                for (int i = drawer.Length - 1; i >= 0; i--)
                {
                    nextdrawer.AddControl(drawer.GetCP(i));
                }
            }
            else
            {
                for (int i = 0; i < drawer.Length; i++)
                {
                    nextdrawer.AddControl(drawer.GetCP(i));
                }
            }
            if (e.Hovered.index == 0)
            {
                for (int i = 0; i < target.Length; i++)
                {
                    nextdrawer.AddControl(target.GetCP(i));
                }
            }
            else
            {
                for (int i = target.Length - 1; i >= 0; i--)
                {
                    nextdrawer.AddControl(target.GetCP(i));
                }
            }
            prev1 = target.ToMinimumData();
            next = nextdrawer.ToMinimumData();
            e.RemovePath(prev0.Id);
            e.RemovePath(prev1.Id);
            return true;
        }

        return false;
    }

    public override void OnFinish(DrawingCanvas e)
    {
        e.TempDrawer.IsVisible = false;
    }

    public override void Redo(DrawingCanvas e)
    {
        if (closing)
        {
            e.GetDrawer(id).SetClosed(true);
            return;
        }

        e.RemovePath(prev0.Id);
        e.RemovePath(prev1.Id);
        e.AddPath(next);
    }

    public override void Undo(DrawingCanvas e)
    {
        if (closing)
        {
            e.GetDrawer(id).SetClosed(false);
            return;
        }

        e.RemovePath(next.Id);
        e.AddPath(prev0);
        e.AddPath(prev1);
    }
}

public class DAClearAll : DrawAction
{
    public override string Name => "CA";

    MinimumCanvasData canvas;

    public override UniTask<bool> Do(DrawingCanvas e)
    {
        canvas = new MinimumCanvasData(e.Drawers.Values);
        e.RemoveAllPath();
        return UniTask.FromResult(true);
    }

    public override void Redo(DrawingCanvas e)
    {
        e.RemoveAllPath();
    }

    public override void Undo(DrawingCanvas e)
    {
        foreach (var d in canvas.Paths)
        {
            e.AddPath(d);
        }
    }
}

public class DAMoveCanvas : DrawAction
{
    public override string Name => "";

    Camera cam;
    int clickbutton;

    public DAMoveCanvas(Camera cam, int clickbutton)
    {
        this.cam = cam;
        this.clickbutton = clickbutton;
    }

    public async override UniTask<bool> Do(DrawingCanvas e)
    {
        var defaultpos = (Vector2)InputMgr.MousePosScreen;
        var prev = (Vector2)cam.transform.position;
        while (Input.GetMouseButton(clickbutton))
        {
            var delta = (Vector2)InputMgr.MousePosScreen - defaultpos;
            delta *= cam.orthographicSize * 2 / Screen.height;
            cam.transform.position = new Vector3(prev.x - delta.x, prev.y - delta.y, cam.transform.position.z);
            await UniTask.Yield();
        }
        return false; //Undo/Redoに記録しない
    }

    public override void Redo(DrawingCanvas e)
    {
        throw new System.NotImplementedException();
    }

    public override void Undo(DrawingCanvas e)
    {
        throw new System.NotImplementedException();
    }
}