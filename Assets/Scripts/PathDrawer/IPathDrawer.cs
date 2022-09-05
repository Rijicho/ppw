using System.Collections.Generic;
using UnityEngine;
using RUtil.Curve;
using UnityEngine.Rendering;

public interface IPathDrawer
{
    int Id { get; set; }
    bool IsDirty { get; }
    int Length { get; }
    bool IsVisible { get; set; }
    bool IsClosed { get; }
    int PlotStepPerSegment { get; }

    MinimumPathData ToMinimumData();

    void RecalcMesh(Camera cam, bool includeCurvature);
    void Render(Camera cam, CommandBuffer buffer, Material mat, ConicPathDrawerOption options);

    void Load(MinimumPathData data);
    void AddControl(ControlPoint control);
    void InsertControl(int index, ControlPoint control);
    void RemoveControlAt(int index);
    void RemoveLastControl();
    void Clear();
    ControlPoint GetCP(int index);
    Vector3 GetPosition(int index);
    float GetWeight(int index);
    void SetPosition(int index, Vector3 pos);
    void SetWeight(int index, float weight);
    void SetClosed(bool isClosed);
    bool IsOnPath(Camera cam, Vector3 v, out float distance, out int segment, out bool isAtSideP, bool updateCurvature);

    IEnumerable<Vector3> GetPlotData();
    IEnumerable<(Vector3 from, Vector3 to, float width)> GetCurvaturePlotData();
}
