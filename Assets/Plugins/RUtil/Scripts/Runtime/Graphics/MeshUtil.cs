using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RUtil.Graphics
{
    public class MeshUtil
    {
        static List<Vector3> vs = new List<Vector3>();
        static List<int> ts = new List<int>() { 0, 1, 2, 2, 3, 0 };
        static List<Vector2> us = new List<Vector2>() { Vector2.up, Vector2.one, Vector2.right, Vector2.zero };
        static List<Color> cs = new List<Color>();

        static void SetParamToMesh(Mesh mesh, List<Vector3> vs, List<int> ts, List<Vector2> us, int vlen, int tlen, int ulen)
        {
            if (mesh.vertexCount > vlen)
            {
                mesh.SetTriangles(ts, 0, tlen, 0);
                mesh.SetVertices(vs, 0, vlen);
            }
            else
            {
                mesh.SetVertices(vs, 0, vlen);
                mesh.SetTriangles(ts, 0, tlen, 0);
            }
            mesh.SetUVs(0, us, 0, ulen);
        }

        static void SetRect(
            List<Vector3> vs, List<int> ts, List<Vector2> us,
            ref int vi, ref int ti, ref int ui,
            Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl)
        {
            while (vs.Count < vi + 4) vs.Add(default);
            while (ts.Count < ti + 6) ts.Add(default);
            while (us.Count < ui + 4) us.Add(default);
            var ofs = vi;
            vs[vi++] = tl;
            vs[vi++] = tr;
            vs[vi++] = br;
            vs[vi++] = bl;
            ts[ti++] = ofs;
            ts[ti++] = ofs + 1;
            ts[ti++] = ofs + 2;
            ts[ti++] = ofs + 2;
            ts[ti++] = ofs + 3;
            ts[ti++] = ofs;
            us[ui++] = Vector2.up;
            us[ui++] = Vector2.one;
            us[ui++] = Vector2.right;
            us[ui++] = Vector2.zero;
        }

        static void SetTriangle(
            List<Vector3> vs, List<int> ts, List<Vector2> us,
            ref int vi, ref int ti, ref int ui,
            Vector3 tl, Vector3 tr, Vector3 bm)
        {
            while (vs.Count < vi + 3) vs.Add(default);
            while (ts.Count < ti + 3) ts.Add(default);
            while (us.Count < ui + 3) us.Add(default);
            var ofs = vi;
            vs[vi++] = tl;
            vs[vi++] = tr;
            vs[vi++] = bm;
            ts[ti++] = ofs;
            ts[ti++] = ofs + 1;
            ts[ti++] = ofs + 2;
            us[ui++] = Vector2.up;
            us[ui++] = Vector2.one;
            us[ui++] = Vector2.one / 2;
        }

        static void SetLine(
            List<Vector3> vs, List<int> ts, List<Vector2> us,
            ref int vi, ref int ti, ref int ui,
            Vector3 from, Vector3 to, Vector3 fromNormal, Vector3 toNormal)
        {
            SetRect(vs, ts, us, ref vi, ref ti, ref ui, from + fromNormal, to + toNormal, to - toNormal, from - fromNormal);
        }

        static void SetFan(List<Vector3> vs, List<int> ts, List<Vector2> us,
            ref int vi, ref int ti, ref int ui,
            Vector3 center, Vector3 beginNormal, Vector3 endNormal, float radius, int segmentPerCircle)
        {
            if (beginNormal == endNormal)
            {
                for (int i = 0; i < segmentPerCircle; i++)
                {
                    var q = Quaternion.Euler(0, 0, -360f / segmentPerCircle * i);
                    var q2 = Quaternion.Euler(0, 0, -360f / segmentPerCircle * (i + 1));
                    SetTriangle(vs, ts, us, ref vi, ref ti, ref ui, center + q * beginNormal * radius, center + q2 * beginNormal * radius, center);
                }
            }
            else
            {
                var angle = Vector3.SignedAngle(beginNormal, endNormal, Vector3.forward);
                if (angle > 0)
                {
                    angle = 360 - angle;
                }
                else
                {
                    angle = -angle;
                }
                int segment = 0;
                while ((float)segment / segmentPerCircle < angle / 360f)
                {
                    segment++;
                }
                for (int i = 0; i < segment; i++)
                {
                    var q = Quaternion.Euler(0, 0, -angle / segment * i);
                    var q2 = Quaternion.Euler(0, 0, -angle / segment * (i + 1));
                    SetTriangle(vs, ts, us, ref vi, ref ti, ref ui, center + q * beginNormal * radius, center + q2 * beginNormal * radius, center);
                }
            }

        }

        public static void Make2DLineMesh(Mesh mesh, Vector3 from, Vector3 to, Vector3 fromNormal, Vector3 toNormal)
        {
            var (vi, ui, ti) = (0, 0, 0);
            SetLine(vs, ts, us, ref vi, ref ti, ref ui, from, to, fromNormal, toNormal);
            SetParamToMesh(mesh, vs, ts, us, vi, ti, ui);
        }


        public static void Make2DLineMesh(Mesh mesh, Vector3 from, Vector3 to, float width = 0.1f)
        {
            var delta = width / 2;
            var dir = (to - from).normalized;
            var normal = new Vector3(-dir.y, dir.x) * delta;
            Make2DLineMesh(mesh, from, to, normal, normal);
        }

        public static void Make2DLinesMesh(Mesh mesh, List<(Vector3 from, Vector3 to, float width)> lines)
        {
            var (vi, ui, ti) = (0, 0, 0);
            foreach (var (from, to, width) in lines)
            {
                var delta = width / 2;
                var dir = (to - from).normalized;
                var normal = new Vector3(-dir.y, dir.x) * delta;
                SetLine(vs, ts, us, ref vi, ref ti, ref ui, from, to, normal, normal);
                SetParamToMesh(mesh, vs, ts, us, vi, ti, ui);
            }
        }

        public static void Make2DFanMesh(Mesh mesh, Vector3 center, Vector3 beginNormal, Vector3 endNormal, float radius, int segmentPerCircle)
        {
            var (vi, ui, ti) = (0, 0, 0);
            SetFan(vs, ts, us, ref vi, ref ui, ref ti, center, beginNormal, endNormal, radius, segmentPerCircle);
            SetParamToMesh(mesh, vs, ts, us, vi, ti, ui);

        }

        public static void Make2DDiscMesh(Mesh mesh, Vector3 center, float radius, int segment)
        {
            var (vi, ui, ti) = (0, 0, 0);
            SetFan(vs, ts, us, ref vi, ref ui, ref ti, center, Vector3.up, Vector3.up, radius, segment);
            SetParamToMesh(mesh, vs, ts, us, vi, ti, ui);
        }


        public static void Make2DPathMesh(Mesh mesh, IList<Vector3> path, int start, int length, float width = 0.1f, bool loop = false)
        {
            if (length < 2)
            {
                throw new ArgumentException("The number of path points must be more than 1.");
            }

            if (length == 2)
            {
                Make2DLineMesh(mesh, path[0], path[1], width);
            }


            var (vi, ui, ti) = (0, 0, 0);

            for (int i = start; i < start + (loop ? length : length - 1); i++)
            {
                bool isLoopAndLast = loop && i == start + length - 1;
                var current = path[i];
                var next = path[isLoopAndLast ? start : i + 1];
                var dir = (next - current).normalized;
                var normal = new Vector3(-dir.y, dir.x);
                var scaledNormal = normal * width / 2;

                if (i == start && !loop)
                    SetFan(vs, ts, us, ref vi, ref ti, ref ui, current, -normal, normal, width / 2, 8);


                var tl = current + scaledNormal;
                var bl = current - scaledNormal;
                var tr = next + scaledNormal;
                var br = next - scaledNormal;
                SetRect(vs, ts, us, ref vi, ref ti, ref ui, tl, tr, br, bl);


                Vector3 nnext;
                if (i == start + length - 2)
                {
                    if (loop)
                    {
                        nnext = path[start];
                    }
                    else
                    {
                        SetFan(vs, ts, us, ref vi, ref ti, ref ui, next, normal, -normal, width / 2, 8);
                        break;
                    }
                }
                else if (i == start + length - 1)
                {
                    nnext = path[1];
                }
                else
                {
                    nnext = path[i + 2];
                }

                var nextdir = (nnext - next).normalized;
                var nextnormal = new Vector3(-nextdir.y, nextdir.x);
                if (dir != nextdir && dir != -nextdir)
                {
                    var angle = Vector3.SignedAngle(-dir, nextdir, Vector3.forward) * Mathf.Deg2Rad;
                    if (angle > 0)
                    {
                        SetFan(vs, ts, us, ref vi, ref ti, ref ui, next, normal, nextnormal, width / 2, 8);
                    }
                    else
                    {
                        SetFan(vs, ts, us, ref vi, ref ti, ref ui, next, -nextnormal, -normal, width / 2, 8);
                    }
                }

            }
            SetParamToMesh(mesh, vs, ts, us, vi, ti, ui);
        }

        public static void Make2DPathMesh(Mesh mesh, float width, bool loop, params Vector3[] path)
        {
            Make2DPathMesh(mesh, path, 0, path.Length, width, loop);
        }

        public static void Make2DRectLineMesh(Mesh mesh, Rect rect, float lineWidth = 0.1f)
        {
            Make2DPathMesh(mesh, lineWidth, true,
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMin, rect.yMin));
        }

        public static void MakeTextMesh(Mesh mesh, TextGenerator tgen, string text, Font font, int fontSize, TextAnchor anchor, Vector3 anchorPos, Color fontColor, float scale, Vector3 rotation)
        {
            var settings = new TextGenerationSettings()
            {
                textAnchor = anchor,
                font = font,
                fontSize = fontSize,
                color = fontColor,
                fontStyle = FontStyle.Normal,
                verticalOverflow = VerticalWrapMode.Overflow,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                alignByGeometry = true,
                richText = false,
                lineSpacing = 1f,
                scaleFactor = 1f,
                resizeTextForBestFit = false,
            };
            tgen.Populate(text, settings);
            ConvertTextGeneratorToMesh(vs, ts, us, cs, mesh, tgen, anchorPos, scale, rotation);
        }

        public static void DrawMesh(Mesh mesh, CommandBuffer buffer, Material mat, MaterialPropertyBlock matprop)
        {
            if (matprop == null)
                buffer.DrawMesh(mesh, Matrix4x4.identity, mat);
            else
                buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, 0, matprop);
        }

        static void ConvertTextGeneratorToMesh(List<Vector3> v, List<int> t, List<Vector2> u, List<Color> c, Mesh mesh, TextGenerator generator, Vector3 anchorPos, float scale, Vector3 rotation)
        {
            var vertexCount = generator.vertexCount;

            while (v.Count < vertexCount * 4) v.Add(default);
            while (t.Count < vertexCount * 6) t.Add(default);
            while (u.Count < vertexCount * 4) u.Add(default);
            while (c.Count < vertexCount * 4) c.Add(default);

            var q = Quaternion.Euler(rotation);
            var uiverts = generator.verts;
            for (var i = 0; i < vertexCount; i += 4)
            {
                for (var j = 0; j < 4; ++j)
                {
                    var idx = i + j;
                    v[idx] = (anchorPos + q * uiverts[idx].position * scale);
                    c[idx] = (uiverts[idx].color);
                    u[idx] = (uiverts[idx].uv0);
                }
                t[i / 4 * 6] = (i);
                t[i / 4 * 6 + 1] = (i + 1);
                t[i / 4 * 6 + 2] = (i + 2);
                t[i / 4 * 6 + 3] = (i + 2);
                t[i / 4 * 6 + 4] = (i + 3);
                t[i / 4 * 6 + 5] = (i);
            }

            mesh.Clear();
            mesh.SetVertices(v, 0, vertexCount);
            mesh.SetUVs(0, u, 0, vertexCount);
            mesh.SetTriangles(t, 0, vertexCount / 4 * 6, 0);
            mesh.SetColors(c, 0, vertexCount);
            mesh.RecalculateBounds();
        }
    }

}

