using UnityEngine;
using RUtil.Curve;

[System.Serializable]
public class MinimumPathData
{
    public int Id;
    public Vector3[] Path;
    public float[] Weights;
    public bool IsClosed;

    public ControlPoint GetCP(int index) => new ControlPoint(Path[index], Weights[index]);

    public MinimumPathData()
    {
        Id = -1;        
    }

    public MinimumPathData(RKCurvesDrawer drawer)
    {
        Id = drawer.Id;
        Path = new Vector3[drawer.Length];
        Weights = new float[drawer.Length];
        for(int i=0; i < drawer.Length; i++)
        {
            Path[i] = drawer.GetPosition(i);
            Weights[i] = drawer.GetWeight(i);
        }
        IsClosed = drawer.IsClosed;
    }

    public MinimumPathData(PPWCurveDrawer drawer)
    {
        Id = drawer.Id;
        Path = new Vector3[drawer.Length];
        Weights = new float[drawer.Length * 3];
        for(int i=0; i<drawer.Length; i++)
        {
            Path[i] = drawer.GetPosition(i);
            Weights[i] = drawer.GetWeight(i);
        }
        for(int i=0; i<drawer.Length; i++)
        {
            Weights[drawer.Length + i] = drawer.GetPhi(i);
        }

        for (int i = 0; i < drawer.Length; i++)
        {
            Weights[drawer.Length * 2 + i] = drawer.GetPsi(i);
        }
        IsClosed = drawer.IsClosed;
    }

    public IPathDrawer Load()
    {
        if (Weights.Length == Path.Length)
            return new RKCurvesDrawer(this);
        else
            return new PPWCurveDrawer(this);
    }
}
