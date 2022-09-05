using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CurveUtility
{


    public class RBezierTest : MonoBehaviour
    {
        [SerializeField] Vector2[] cps = new Vector2[3] { Vector2.zero, Vector2.up, Vector2.one };
        [SerializeField] float w = 1;
        [SerializeField] bool isShowBezierControl = false;
        [SerializeField] int stepPerSegment = 20;
        [SerializeField] float currentT = 0.5f;

        public Vector3 Z;

        public Vector2 Calc(float t, float _w)
        {
            var (c0, c1, c2) = (cps[0], cps[1], cps[2]);
            var s = 1 - t;
            return (s * s * c0 + 2 * t * s * _w * c1 + t * t * c2) / (s * s + 2 * t * s * _w + t * t);
        }



        public float CheckCurvature(float t)
        {
            var (c01, c21) = (cps[0] - cps[1], cps[2] - cps[1]);
            var s = 1 - t;
            var ft = -w * c01 * s * s + (c21 - c01) * t * s + w * c21 * t * t;
            var ftd = 2 * w * c01 * s + (c21 - c01) * (s - t) + 2 * w * c21 * t;
            var w02 = s * s + 2 * w * t * s + t * t;
            var w02d = 2 * (w - 1) * (1 - 2 * t);
            return Vector2.Dot(ft, ftd) / ft.sqrMagnitude - w02d / w02;
        }



        private static void Inv(float[,] src, float[,] inv)
        {
            var dby =
                  src[0, 0] * src[1, 1] * src[2, 2]
                + src[0, 1] * src[1, 2] * src[2, 0]
                + src[0, 2] * src[1, 0] * src[2, 1]
                - src[0, 2] * src[1, 1] * src[2, 0]
                - src[0, 1] * src[1, 0] * src[2, 2]
                - src[0, 0] * src[1, 2] * src[2, 1];


            for (int i = 0, pi = 2, ni = 1; i < 3; i++, pi = (pi + 1) % 3, ni = (ni + 1) % 3)
            {
                for (int j = 0, pj = 2, nj = 1; j < 3; j++, pj = (pj + 1) % 3, nj = (nj + 1) % 3)
                {
                    inv[j, i] = (src[ni, nj] * src[pi, pj] - src[ni, pj] * src[pi, nj]) / dby;
                }
            }
        }

        private static void Mul(float[,] a, float[,] b, float[,] result)
        {
            for(int i=0; i<3; i++)
            {
                for(int j=0; j<3; j++)
                {
                    result[i, j] = 0;
                    for(int k=0; k<3; k++)
                    {
                        result[i, j] += a[i, k] * b[k, j];
                    }
                }
            }
        }



#if UNITY_EDITOR

        [MenuItem("Tools/TestInv")]
        static void TestInv()
        {
            float[,] src = new float[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    src[i, j] = Random.value;

            float[,] inv = new float[3, 3];
            float[,] result = new float[3, 3];
            Inv(src, inv);
            Debug.Log($"{src[0, 0]},{src[0, 1]},{src[0, 2]}\n{src[1, 0]},{src[1, 1]},{src[1, 2]}\n{src[2, 0]},{src[2, 1]},{src[2, 2]}");
            Debug.Log($"{inv[0, 0]},{inv[0, 1]},{inv[0, 2]}\n{inv[1, 0]},{inv[1, 1]},{inv[1, 2]}\n{inv[2, 0]},{inv[2, 1]},{inv[2, 2]}");
            Mul(src, inv, result);
            Debug.Log($"{result[0, 0]},{result[0, 1]},{result[0, 2]}\n{result[1, 0]},{result[1, 1]},{result[1, 2]}\n{result[2, 0]},{result[2, 1]},{result[2, 2]}");
        }

        float CalcK(float t)
        {
            //三角形の面積を求める関数
            float TriArea(Vector2 p1, Vector2 p2, Vector2 p3)
            {
                p1 -= p3; p2 -= p3;
                return (p1.x * p2.y - p2.x * p1.y) / 2f;
            }

            var (c0, c1, c2) = (cps[0], cps[1], cps[2]);
            var s = 1 - t;
            var a = s * s + 2 * w * t * s + t * t;
            var b = (w * (c1 - c0) * s * s + (c2 - c0) * t * s + w * (c2 - c1) * t * t).magnitude;
            var ab = a / b;
            return w * TriArea(c0, c1, c2) * ab * ab * ab;
        }
        private void OnDrawGizmosSelected ()
        {
            if (isShowBezierControl)
            {
                Gizmos.color = new Color(1, 1, 0, 0.3f);
                Gizmos.DrawLine(cps[0], cps[1]);
                Gizmos.DrawLine(cps[1], cps[2]);
            }
            Handles.color = Color.green;

            Vector2 prev = cps[0];
            Vector2 prevr = cps[0];
            for (int i = 1; i <= stepPerSegment; i++)
            {
                var t = (float)i / stepPerSegment;
                var next = Calc(t, w);
                var nextr = Calc(t, -w);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(prev, next);
                Gizmos.color = new Color(1, 1, 1, 0.2f);
                Gizmos.DrawLine(prevr, nextr);

                var kappa = CalcK(t);
                var delta = CheckCurvature(t);
                var kappaLineEnd = (Vector3)next + Quaternion.Euler(0, 0, -90) * (next - prev).normalized * kappa * 0.5f;
                Gizmos.color = delta > 0 ? new Color(1, 0, 0, 0.3f) : new Color(0, 1f, 1, 0.3f);
                Gizmos.DrawLine(next, kappaLineEnd);
                prev = next;
                prevr = nextr;
            }




            var P = (Vector3)cps[0];
            var Q = (Vector3)cps[1];
            var R = (Vector3)cps[2];


            var ZP = P - Z;
            var ZQ = Q - Z;
            var ZR = R - Z;

            var vp = P - Q;
            var vr = R - Q;
            var vq = P - R;
            var (vps, vqs, vrs) = (vp.sqrMagnitude, vq.sqrMagnitude, vr.sqrMagnitude);

            Vector3 BackCoord(Vector3 v)
            {
                v /= (v.x + v.y + v.z);
                return Z + v.x * ZP + v.y * ZQ + v.z * ZR;
            }



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

            //Debug.Log(Mathf.Sqrt(kl1.z / (kl1.x + kl1.z + 2 * Mathf.Sqrt(kl1.x * kl1.z))));

            var L = kl1;

            //頂点におけるt
            var (v0, v2) = (L.x, L.z);
            var vt = (2 * w * v2 + 1) / (2 * w * (v0 + v2 + 2 * Mathf.Sqrt(v0 * v2)));            
            if (float.IsNaN(vt))
            {
                Debug.Log("NaN");
            }

            L /= (L.x + L.y + L.z);
            var (l0, l1, l2) = (L.x, L.y, L.z);
            var LP = P - BackCoord(L);
            var LR = R - BackCoord(L);

            var C = new Vector3(l0 * (l0 - l2 - 1), l1, l2 * (l2 - l0 - 1));
            C /= (C.x + C.y + C.z);
            C = Z + C.x * ZP + C.y * (BackCoord(L) - Z) + C.z * ZR;
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(C, 0.03f);

            //var ip = Vector3.Dot(l0 * (l2 - l0 + 1) * LP + l2 * (l0 - l2 + 1) * LR, l0 * LP - l2 * LR);
            //Debug.Log(ip);

            float aa = l0 + l2;
            float bb = l0 - l2;
            var mp = LP.magnitude;
            var mr = LR.magnitude;
            var smp = LP.sqrMagnitude;
            var smr = LR.sqrMagnitude;
            var check = (LP+LR).sqrMagnitude * bb * bb * bb 
                - (smp - smr) * (1 - 2 * aa) * bb * bb
                + ((LP-LR).sqrMagnitude * aa - 2 * (smp + smr)) * aa * bb
                - (smp - smr) * aa * aa;
            
            float f(float _a, float _b) => (1 - ww) * _a * _a - 2 * _a + 1 + ww * _b * _b;
            float g(float _a, float _b) => (LP + LR).sqrMagnitude * _b * _b * _b
                - (smp - smr) * (1 - 2 * _a) * _b * _b
                + ((LP - LR).sqrMagnitude * _a - 2 * (smp + smr)) * _a * _b
                - (smp - smr) * _a * _a;
            float fda(float _a, float _b) => 2 * (1 - ww) * _a - 2;
            float fdb(float _a, float _b) => 2 * ww * _b;
            float gda(float _a, float _b) => 2 * (smp - smr) * _b * _b + 2 * _b * (LP - LR).sqrMagnitude * _a - 2 * _b * (smp + smr) - 2 * (smp - smr) * _a;
            float gdb(float _a, float _b) => 3 * (LP + LR).sqrMagnitude * _b * _b - 2 * (smp - smr) * (1 - 2 * _a) * _b + ((LP - LR).sqrMagnitude * _a - 2 * (smp + smr)) * _a;

            var nowa = 1/(1+w);
            var nowb = 0f;
            for(int i=0; i<50; i++)
            {
                var rf = f(nowa, nowb);
                var rg = g(nowa, nowb);
                var rfda = fda(nowa, nowb);
                var rfdb = fdb(nowa, nowb);
                var rgda = gda(nowa, nowb);
                var rgdb = gdb(nowa, nowb);
                var dby = rfda * rgdb - rfdb * rgda;
                nowa -= (rgdb * rf - rfdb * rg) / dby;
                nowb -= (-rgda * rf + rfda * rg) / dby;
            }
            var rl0 = (nowa + nowb) / 2;
            var rl2 = (nowa - nowb) / 2;
            var rl1 = 1 - rl0 - rl2;
            rl0 /= (rl0 + rl1 + rl2);
            rl1 /= (rl0 + rl1 + rl2);
            rl2 /= (rl0 + rl1 + rl2);
            var calcL = BackCoord(new Vector3(rl0, rl1, rl2));
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(calcL, 0.1f);

            var calcQ = new Vector3(-rl0, 1, -rl2);
            calcQ /= (calcQ.x + calcQ.y + calcQ.z);
            calcQ = Z + calcQ.x * ZP + calcQ.y * (BackCoord(L) - Z) + calcQ.z * ZR;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(calcQ, 0.1f);


            //交点を元の座標へ変換
            kl1 = BackCoord(kl1);
            kl2 = BackCoord(kl2);
            ks1 = BackCoord(ks1);
            ks2 = BackCoord(ks2);

            //軸を描画
            if (w == 1)
            {
                var dir = (vp + vr).normalized * 5;
                Gizmos.color = new Color(1, 1, 1, 0.2f);
                Gizmos.DrawLine(kl1 - dir, kl1 + dir);
            }
            else
            {
                Gizmos.color = new Color(1, 0.5f, 0.5f, 0.2f);
                Gizmos.DrawLine(kl1, kl2);
                Gizmos.color = new Color(0.5f, 0.5f, 1, 0.2f);
                Gizmos.DrawLine(ks1, ks2);
            }

            //軸との交点を描画
            Gizmos.color = new Color(1, 1, 0);
            Gizmos.DrawSphere(kl1, 0.04f);
            Handles.Label(kl1, vt.ToString());
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(kl2, 0.02f);
            Gizmos.DrawSphere(ks1, 0.02f);
            Gizmos.DrawSphere(ks2, 0.02f);

            //中心を描画
            var center = new Vector3(0.5f, -ww, 0.5f);
            center = BackCoord(center);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(center, 0.02f);
        }

        [CustomEditor(typeof(RBezierTest))]
        public class RBezierTestEditor : Editor
        {
            SerializedProperty cps;

            private void OnEnable()
            {
                cps = serializedObject.FindProperty("cps");
            }

            private void OnSceneGUI()
            {
                serializedObject.Update();

                for (int i = 0; i < cps.arraySize; i++)
                {
                    var elem = cps.GetArrayElementAtIndex(i);
                    var prev = (Vector3)elem.vector2Value;
                    Handles.color = new Color(1, 1, 0, 0.7f);
                    Handles.DrawSolidDisc(prev, Vector3.back, 0.02f);
                    Handles.color = new Color(1, 0, 1, 0.5f);
                    var next = Handles.Slider2D(prev, Vector3.back, Vector3.right, Vector3.up, 0.1f, Handles.CircleHandleCap, 0.5f);
                    if (prev != next)
                    {
                        Undo.RecordObject(target, "move");
                        elem.vector2Value = next;
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }
        }



#endif
    }
}