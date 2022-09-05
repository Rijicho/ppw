using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RUtil.Curve
{
    public static partial class PPWCurve
    {
        public sealed class CurveData
        {
            /// <summary>
            /// The max number of control-points
            /// </summary>
            public int NMax { get; private set; }
            /// <summary>
            /// The number of valid control-points
            /// </summary>
            public int ValidCPCount { get => n; set => n = Mathf.Clamp(value, 0, NMax); }
            int n;
            /// <summary>
            /// Input control points with weight coefficients
            /// </summary>
            public List<ControlPoint> ControlPoints { get; private set; }
            public ControlPoint this[int index] => ControlPoints[index];
            /// <summary>
            /// Input fitting coefficients
            /// </summary>
            public List<float> Phis { get; set; }
            /// <summary>
            /// Input biasing coefficients
            /// </summary>
            public List<float> Psis { get; set; }

            /// <summary>
            /// Is the curve closed or not
            /// </summary>
            public bool IsClosed { get; set; }

            /// <summary>
            /// How many plotting points are required for each segment
            /// </summary>
            public int PlotStepPerSegment { get; private set; }

            /// <summary>
            /// The interpolated lines before blending.
            /// For non-closed curve, Polygons[0]'s latter half and Polygons[n-1]'s former half have valid plots.
            /// </summary>
            public List<List<Vector3>> Polygons { get; private set; }

            /// <summary>
            /// Plotted points. The valid range is given by ValidPlotLength.
            /// </summary>
            public List<Vector3> Plots { get; private set; }

            /// <summary>
            /// The count of valid points in Plots
            /// </summary>
            public int ValidPlotLength => CalcPlotLength(n, PlotStepPerSegment, IsClosed);
            int MaxPlotLength => CalcPlotLength(NMax, PlotStepPerSegment, IsClosed);
            int CalcPlotLength(int cpCount, int stepPerSegment, bool isClosed) =>
                cpCount == 0 ? 0
                : cpCount == 1 ? 1
                : cpCount == 2 ? 2
                : isClosed ? (stepPerSegment * cpCount)
                : stepPerSegment * (cpCount - 1) + 1;

            /// <summary>
            /// How many segments the curve has
            /// </summary>
            public int SegmentCount => IsClosed ? ValidCPCount : (ValidCPCount - 1);

            /// <summary>
            /// Allocator
            /// </summary>
            /// <param name="max">The number of user's control-points</param>
            public CurveData(int max, int stepPerSegment)
            {
                NMax = Mathf.Max(1, max);
                PlotStepPerSegment = stepPerSegment;
                EnsureAllocated();
            }

            public void Init(IEnumerable<ControlPoint> ps, IEnumerable<float> phis, IEnumerable<float> psis, int stepPerSegment)
            {
                PlotStepPerSegment = stepPerSegment;
                var len = ps.Count();
                n = len;

                int _n = 2;
                while (_n < len)
                {
                    _n *= 2;
                }
                NMax = _n;
                EnsureAllocated();

                int i = 0;
                foreach(var pi in ps)
                {
                    ControlPoints[i++] = pi;
                }
                i = 0;
                foreach(var phi in phis)
                {
                    Phis[i++] = phi;
                }
                i = 0;
                foreach (var psi in psis)
                {
                    Psis[i++] = psi;
                }
            }

            public void EnsureAllocated()
            {
                if (ControlPoints is null) ControlPoints = new List<ControlPoint>();
                if (Polygons is null) Polygons = new List<List<Vector3>>();
                if (Phis is null) Phis = new List<float>();
                if (Psis is null) Psis = new List<float>();
                if (Plots is null) Plots = new List<Vector3>();

                while (ControlPoints.Count < NMax) ControlPoints.Add(default);
                while (ControlPoints.Count > NMax) ControlPoints.RemoveAt(ControlPoints.Count - 1);
                while (Polygons.Count < NMax) Polygons.Add(new List<Vector3>());
                while (Polygons.Count > NMax) Polygons.RemoveAt(Polygons.Count - 1);
                foreach (var tp in Polygons)
                {
                    while(tp.Count < PlotStepPerSegment * 2 + 1) tp.Add(default);
                    while (tp.Count > PlotStepPerSegment * 2 + 1) tp.RemoveAt(tp.Count - 1);
                }
                while (Phis.Count < NMax) Phis.Add(1);
                while (Phis.Count > NMax) Phis.RemoveAt(Phis.Count - 1);
                while (Psis.Count < NMax) Psis.Add(0);
                while (Psis.Count > NMax) Psis.RemoveAt(Psis.Count - 1);
                while (Plots.Count < MaxPlotLength) Plots.Add(default);
                while (Plots.Count > MaxPlotLength) Plots.RemoveAt(Plots.Count - 1);
            }

            public void Plot()
            {
                CalcAll(this);
            }

            public bool NeedAutoPlot { get; set; } = true;
            void AutoUpdate()
            {
                var _n = Mathf.Max(2, NMax);
                while (n >= _n)
                    _n *= 2;
                NMax = _n;
                EnsureAllocated();

                if (NeedAutoPlot)
                    Plot();
            }


            #region Edit
            public void AddControl(ControlPoint control)
            {
                if (n == ControlPoints.Count)
                    ControlPoints.Add(control);
                else
                    ControlPoints[n] = control;
                n++;
                AutoUpdate();
            }
            public void AddControls(IEnumerable<ControlPoint> controls)
            {
                foreach(var c in controls)
                {
                    AddControl(c);
                }
            }
            public void InsertControl(int index, ControlPoint control)
            {
                if (index < 0)
                    index += ControlPoints.Count;

                ControlPoints.Insert(index, control);
                Phis.Insert(index, 1);
                Psis.Insert(index, 0);
                n++;
                AutoUpdate();
            }

            public void RemoveLastControl()
            {
                ControlPoints.RemoveAt(ControlPoints.Count - 1);
                Phis.RemoveAt(Phis.Count - 1);
                Psis.RemoveAt(Psis.Count - 1);
                n--;
                AutoUpdate();
            }
            public void RemoveControlAt(int index)
            {
                if (index < 0)
                    index += ControlPoints.Count;
                ControlPoints.RemoveAt(index);
                Phis.RemoveAt(index);
                Psis.RemoveAt(index);
                n--;
                AutoUpdate();
            }

            public void Clear()
            {
                ControlPoints.Clear();
                Phis.Clear();
                Psis.Clear();
                n = 0;
                AutoUpdate();
            }
            #endregion

            #region get/set
            public ControlPoint GetCP(int index)
            {
                if (index < 0)
                    index += ValidCPCount;
                return ControlPoints[index];
            }
            public Vector3 GetPosition(int index)
            {
                if (index < 0)
                    index += ValidCPCount;
                return ControlPoints[index].Position;
            }
            public float GetWeight(int index)
            {
                if (index < 0)
                    index += ValidCPCount;
                return ControlPoints[index].TargetWeight;
            }
            public float GetPhi(int index)
            {
                if (index < 0)
                    index += ValidCPCount;
                return Phis[index];
            }
            public float GetPsi(int index)
            {
                if (index < 0)
                    index += ValidCPCount;
                return Psis[index];
            }
            public bool SetCP(int index, ControlPoint cp)
            {
                if (index < 0)
                    index += ValidCPCount;
                var prev = ControlPoints[index];
                ControlPoints[index] = cp;
                return prev != ControlPoints[index];
            }
            public bool SetPosition(int index, Vector3 position)
            {
                if (index < 0)
                    index += ValidCPCount;
                var prev = ControlPoints[index].Position;
                ControlPoints[index] = new ControlPoint(position, ControlPoints[index].TargetWeight);
                return prev != ControlPoints[index].Position;
            }
            public bool SetWeight(int index, float weight)
            {
                if (index < 0)
                    index += ValidCPCount;
                var prev = ControlPoints[index].TargetWeight;
                ControlPoints[index] = new ControlPoint(ControlPoints[index].Position, weight);
                return prev != ControlPoints[index].TargetWeight;
            }
            public bool SetPhi(int index, float phi)
            {
                if (index < 0)
                    index += ValidCPCount;
                var prev = Phis[index];
                Phis[index] = phi;
                return prev != Phis[index];
            }

            public bool SetPsi(int index, float psi)
            {
                if (index < 0)
                    index += ValidCPCount;
                var prev = Psis[index];
                Psis[index] = psi;
                return prev != Psis[index];
            }
            #endregion

        }

    }
}
