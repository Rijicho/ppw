using RUtil.Mathematics;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace RUtil.Curve
{
    public static partial class RKCurves
    {
        [Serializable]
        public class CurveData
        {
            [SerializeField] List<ControlPoint> userControls;
            BezierControls bezierControls;

            public ControlPoint[] ControlPolygon => bezierControls.Points;

            public int SegmentCount => userControls.Count;

            public Vector3[] Plots => plots;
            Vector3[] plots;

            public int ValidPlotLength { get; private set; }
            public int ValidBezierControlLength => SegmentCount * 2 + (IsClosed ? 0 : 1);
            public int ValidUserControlLength => userControls.Count;

            public int Iteration { get; set; } = 8;
            public bool IsClosed { get; set; } = false;
            public int PlotStepPerSegment { get; set; } = 32;

            public bool NeedAutoCalc { get; set; }
            public bool NeedAutoPlot { get; set; }

            public CurveData()
            {
                userControls = new List<ControlPoint>();
                Iteration = 8;
                IsClosed = false;
                PlotStepPerSegment = 32;
                NeedAutoCalc = false;
                NeedAutoPlot = false;
            }

            /// <summary>
            /// Clone
            /// </summary>
            /// <param name="src"></param>
            public CurveData(CurveData src)
            {
                userControls = new List<ControlPoint>(src.userControls);
                Iteration = src.Iteration;
                IsClosed = src.IsClosed;
                PlotStepPerSegment = src.PlotStepPerSegment;
                NeedAutoCalc = src.NeedAutoCalc;
                NeedAutoPlot = src.NeedAutoPlot;
            }


            public void CalcBezierControls()
            {
                if (bezierControls == null)
                {
                    int size = 2;
                    while (size < userControls.Count)
                    {
                        size *= 2;
                    }
                    bezierControls = new BezierControls(size, userControls.Count);
                }

                RKCurves.CalcBezierControls(userControls, Iteration, IsClosed, bezierControls);
            }

            public void Plot()
            {
                if (plots == null)
                    plots = Array.Empty<Vector3>();
                ValidPlotLength = CalcPlots(bezierControls, IsClosed, ref plots, PlotStepPerSegment);
            }

            private void AutoUpdate()
            {
                if (NeedAutoCalc)
                    CalcBezierControls();
                if (NeedAutoPlot)
                    Plot();
            }

            #region indexer
            /// <summary>
            /// κ-Conics control point (input)
            /// </summary>
            /// <param name="i">Segment index</param>
            /// <returns></returns>
            public ControlPoint this[int i] { get => userControls[i]; set => userControls[i] = value; }

            /// <summary>
            /// Bezier control point (output)
            /// </summary>
            /// <param name="i">Segment index</param>
            /// <param name="j">Point index</param>
            /// <returns></returns>
            public ControlPoint this[int i, int j] => new ControlPoint(bezierControls[i, j].Position, j == 1 ? bezierControls[i, 1].TargetWeight : 1, j == 1 ? bezierControls[i, 1].ModifiedWeight : 1);
            #endregion

            #region Edit
            public void AddControl(ControlPoint control)
            {
                userControls.Add(control);
                AutoUpdate();
            }
            public void AddControls(IEnumerable<ControlPoint> controls)
            {
                userControls.AddRange(controls);
                AutoUpdate();
            }
            public void InsertControl(int index, ControlPoint control)
            {
                if (index < 0)
                    index += userControls.Count;

                userControls.Insert(index, control);
                AutoUpdate();
            }

            public void RemoveLastControl()
            {
                userControls.RemoveAt(userControls.Count - 1);
                AutoUpdate();
            }
            public void RemoveControlAt(int index)
            {
                if (index < 0)
                    index += userControls.Count;
                userControls.RemoveAt(index);
                AutoUpdate();
            }

            public void Clear()
            {
                userControls.Clear();
                AutoUpdate();
            }
            #endregion

            #region get/set
            public ControlPoint GetControlPolygon(int i) => new ControlPoint(bezierControls[i].Position, i % 2 == 1 ? userControls[i / 2].TargetWeight : 1);
            public ControlPoint GetCP(int index)
            {
                if (index < 0)
                    index += userControls.Count;
                return userControls[index];
            }
            public Vector3 GetPosition(int index)
            {
                if (index < 0)
                    index += userControls.Count;
                return userControls[index].Position;
            }
            public float GetWeight(int index)
            {
                if (index < 0)
                    index += userControls.Count;
                return userControls[index].TargetWeight;
            }
            public bool SetCP(int index, ControlPoint cp)
            {
                if (index < 0)
                    index += userControls.Count;
                var prev = userControls[index];
                userControls[index] = cp;
                return prev != userControls[index];
            }
            public bool SetPosition(int index, Vector3 position)
            {
                if (index < 0)
                    index += userControls.Count;
                var prev = userControls[index].Position;
                userControls[index] = new ControlPoint(position, userControls[index].TargetWeight);
                return prev != userControls[index].Position;
            }
            public bool SetWeight(int index, float weight)
            {
                if (index < 0)
                    index += userControls.Count;
                var prev = userControls[index].TargetWeight;
                userControls[index] = new ControlPoint(userControls[index].Position, weight);
                return prev != userControls[index].TargetWeight;
            }
            #endregion

            #region Utility

            static bool Inverse(ref Vector3 p, ref Vector3 q, ref Vector3 r)
            {
                var dby = p.x * q.y * r.z + p.y * q.z * r.x + p.z * q.x * r.z - p.z * q.y * r.x - p.y * q.x * r.z - p.x * q.z * r.y;
                if (dby == 0)
                    return false;
                var tp = new Vector3(q.y * r.z - q.z - r.y, -p.y * r.z + p.z * r.y, p.y * q.z - p.z * q.y);
                var tq = new Vector3(-q.x * r.z + q.z * r.x, p.x * r.z - p.z * r.x, -p.x * q.z + p.z * q.x);
                var tr = new Vector3(q.x * r.y - q.y * r.x, -p.x * r.y + p.y * r.x, p.x * q.y - p.y * q.x);
                p = tp / dby;
                q = tq / dby;
                r = tr / dby;
                return true;
            }

            static Vector3 Mul(Vector3 ip, Vector3 iq, Vector3 ir, Vector3 x)
            {
                return new Vector3(Vector3.Dot(ip, x), Vector3.Dot(iq, x), Vector3.Dot(ir, x));
            }

            static bool IsOnLine(Vector3 from, Vector3 to, Vector3 v, float error)
            {
                var v0 = to - from;
                var v1 = v - from;
                var v2 = v - to;
                if (Vector3.Dot(v1, v2) > 0)
                    return false;
                if (Mathf.Abs(Vector3.Dot(v0.normalized, v1.normalized) - 1) < error)
                    return true;

                return false;
            }


            public bool IsOnSegment(int index, Vector3 v, float range, out float distance, out bool isAtSideP)
            {
                var (op, oq, or) = (this[index, 0].Position, this[index, 1].Position, this[index, 2].Position);

                //直線PRよりQ側にある場合のみ処理する
                var (rq, rp, rv) = (oq - or, op - or, v - or);
                var (c0, c1) = (Vector3.Cross(rp, rv).normalized, Vector3.Cross(rp, rq).normalized);
                if (Vector3.Dot(c0, c1) < -0.1f)
                {
                    isAtSideP = false;
                    distance = -1;
                    return false;
                }


                var w = bezierControls[index, 1].ModifiedWeight;
                var (f0, f1) = GetFoci(index);

                if (w == 1)
                {
                    var (p, r) = (op - oq, or - oq);
                    var sp = p.sqrMagnitude;
                    var sr = r.sqrMagnitude;
                    var pr = Vector3.Dot(p, r);
                    var d0 = new Vector3(0, sr / (pr + sr), pr / (pr + sr));
                    var d1 = new Vector3(pr / (pr + sp), sp / (pr + sp), 0);
                    d0 = BackCoord(d0, op, oq, or);
                    d1 = BackCoord(d1, op, oq, or);
                    var u0 = d0 - v;
                    var u1 = d1 - v;
                    var h = Mathf.Sqrt(Vector3.Cross(u0, u1).sqrMagnitude / (d0 - d1).sqrMagnitude);
                    var h2 = (f0 - v).magnitude;
                    distance = Mathf.Abs(h - h2);
                    isAtSideP = Mathf.Sign(Vector3.Dot(v - f0, d0 - d1)) * Mathf.Sign(Vector3.Dot(op - f0, d0 - d1)) == 1;
                    return distance < range;
                }
                else if (w < 1)
                {
                    var sample = this[index, 0].Position;
                    var target = (sample - f0).magnitude + (sample - f1).magnitude;
                    var real = (v - f0).magnitude + (v - f1).magnitude;
                    distance = Mathf.Abs(real - target);

                    var adir = (f0 - f1).normalized;
                    (c0, c1) = (Vector3.Cross(adir, v - f1), Vector3.Cross(adir, op - f1));
                    isAtSideP = Vector3.Dot(c0, c1) > 0;
                    return distance < range;
                }
                else
                {
                    if ((f0 - f1).sqrMagnitude < range)
                    {
                        float dpq, drq;
                        var onPQ = IsOnLine(v, op, oq, range, out dpq);
                        var onPR = IsOnLine(v, or, oq, range, out drq);

                        if (onPQ && onPR)
                        {
                            isAtSideP = dpq < drq;
                            distance = isAtSideP ? dpq : drq;
                            return true;
                        }
                        else if (onPQ)
                        {
                            isAtSideP = true;
                            distance = dpq;
                            return true;
                        }
                        else if (onPR)
                        {
                            isAtSideP = false;
                            distance = drq;
                            return true;
                        }
                        else
                        {
                            isAtSideP = false;
                            distance = -1;
                            return false;
                        }
                    }


                    var sample = this[index, 0].Position;
                    var target = (sample - f0).magnitude - (sample - f1).magnitude;
                    var real = (v - f0).magnitude - (v - f1).magnitude;
                    distance = Mathf.Abs(real - target);

                    var adir = (f0 - f1).normalized;
                    (c0, c1) = (Vector3.Cross(adir, v - f1), Vector3.Cross(adir, op - f1));
                    isAtSideP = Vector3.Dot(c0, c1) > 0;

                    return distance < range;
                }
            }

            public bool IsOnLine(Vector3 v, Vector3 from, Vector3 to, float error, out float distance)
            {
                var dir = (to - from).normalized;
                var (a, b) = (from, to);
                if (Mathf.Approximately(Vector3.Distance(a, b), 0))
                {
                    distance = Vector3.Distance(this[0], v);
                    return distance < error;
                }
                if (Vector3.Dot(v - a, dir) >= -error && Vector3.Dot(v - b, -dir) >= -error)
                {
                    distance = Mathf.Sqrt(Vector3.Cross(a - v, b - v).sqrMagnitude / (a - b).sqrMagnitude);
                    return distance < error;
                }
                distance = -1;
                return false;
            }

            public bool IsOnPath(Vector3 v, float error, out float distance, out int segment, out bool isAtSideP)
            {
                distance = -1;
                segment = -1;
                isAtSideP = false;
                if (SegmentCount == 0)
                    return false;

                if (SegmentCount == 1)
                {
                    distance = Vector3.Distance(this[0], v);
                    segment = 0;
                    return distance < error;
                }

                if (SegmentCount == 2)
                {
                    segment = 1;
                    isAtSideP = true;
                    return IsOnLine(v, this[0], this[1], error, out distance);
                }

                for (int i = 0; i < SegmentCount; i++)
                {
                    if (IsOnSegment(i, v, error, out distance, out isAtSideP))
                    {
                        segment = i;
                        return true;
                    }
                }
                return false;
            }

            public (Vector3 f0, Vector3 f1) GetFoci(int i)
            {
                var (p, q, r) = (bezierControls[i, 0], bezierControls[i, 1], bezierControls[i, 2]);
                var w = bezierControls[i, 1].ModifiedWeight;


                if (w == 1)
                {
                    var qp = p - q;
                    var qr = r - q;
                    return ((qr.sqrMagnitude * p + 2 * Vector3.Dot(qp, qr) * q + qp.sqrMagnitude * r) / (qp + qr).sqrMagnitude, Vector3.zero);
                }

                var ww = w * w;

                var a = new Complex(r.x - q.x, r.y - q.y);
                var b = new Complex(p.x - r.x, p.y - r.y);
                var c = new Complex(q.x - p.x, q.y - p.y);
                var sq = Complex.Sqrt(b * b - 4 * ww * a * c);
                var ap = (b + sq) / (2 * w * c);
                var am = (b - sq) / (2 * w * c);

                if (0 <= ap.a && ap.a <= 1)
                    return (
                        (ap.sqrMagnitude * p + 2 * w * ap.a * q + r) / (ap.sqrMagnitude + 2 * w * ap.a + 1),
                        (am.sqrMagnitude * p + 2 * w * am.a * q + r) / (am.sqrMagnitude + 2 * w * am.a + 1));
                else
                    return (
                        (am.sqrMagnitude * p + 2 * w * am.a * q + r) / (am.sqrMagnitude + 2 * w * am.a + 1),
                        (ap.sqrMagnitude * p + 2 * w * ap.a * q + r) / (ap.sqrMagnitude + 2 * w * ap.a + 1));


            }

            public (Vector3 vertex, Vector3 dir) GetAxis(int i, float? weight = null)
            {

                var (P, Q, R) = (bezierControls[i, 0], bezierControls[i, 1], bezierControls[i, 2]);

                var vp = P - Q;
                var vr = R - Q;
                var vq = P - R;
                var (vps, vqs, vrs) = (vp.sqrMagnitude, vq.sqrMagnitude, vr.sqrMagnitude);
                var w = weight ?? bezierControls[i, 1].ModifiedWeight;
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
                kl1 = BackCoord(kl1, P, Q, R);
                kl2 = BackCoord(kl2, P, Q, R);
                ks1 = BackCoord(ks1, P, Q, R);
                ks2 = BackCoord(ks2, P, Q, R);

                Vector3 dir;
                if (w != 1)
                {
                    dir = (kl2 - kl1).normalized;
                }
                else
                {
                    dir = (vp + vr).normalized;
                }

                return (kl1, dir);
            }

            static Vector3 BackCoord(Vector3 v, Vector3 P, Vector3 Q, Vector3 R)
            {
                v /= (v.x + v.y + v.z);
                return v.x * P + v.y * Q + v.z * R;
            }

            #endregion
        }


    }


}
