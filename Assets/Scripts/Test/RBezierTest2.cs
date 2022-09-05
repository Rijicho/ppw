using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CurveUtility
{
    public class RBezierTest2 : MonoBehaviour
    {
        [SerializeField] Vector2[] cps = new Vector2[3] { Vector2.zero, Vector2.up, Vector2.one };
        [SerializeField] float w = 1;
        [SerializeField] bool isShowBezierControl = false;
        [SerializeField] int stepPerSegment = 20;

        public Vector3 Z;

        public Vector2 Calc(Vector2 c0, Vector2 c1, Vector2 c2, float t, float _w)
        {
            var s = 1 - t;
            return (s * s * c0 + 2 * t * s * _w * c1 + t * t * c2) / (s * s + 2 * t * s * _w + t * t);
        }


#if UNITY_EDITOR


        private void OnDrawGizmos()
        {
            Handles.color = Color.green;

            Vector3 P = cps[0];
            Vector3 L = cps[1];
            Vector3 R = cps[2];

            Vector3 p = P - L;
            Vector3 r = R - L;
            var ww = w * w;
            var smp = p.sqrMagnitude;
            var smr = r.sqrMagnitude;

            float f(float _a, float _b) => (1 - ww) * _a * _a - 2 * _a + 1 + ww * _b * _b;
            float g(float _a, float _b) => (p + r).sqrMagnitude * _b * _b * _b
                - (smp - smr) * (1 - 2 * _a) * _b * _b
                + ((p - r).sqrMagnitude * _a - 2 * (smp + smr)) * _a * _b
                - (smp - smr) * _a * _a;
            float fda(float _a, float _b) => 2 * (1 - ww) * _a - 2;
            float fdb(float _a, float _b) => 2 * ww * _b;
            float gda(float _a, float _b) => 2 * (smp - smr) * _b * _b + 2 * _b * (p - r).sqrMagnitude * _a - 2 * _b * (smp + smr) - 2 * (smp - smr) * _a;
            float gdb(float _a, float _b) => 3 * (p + r).sqrMagnitude * _b * _b - 2 * (smp - smr) * (1 - 2 * _a) * _b + ((p - r).sqrMagnitude * _a - 2 * (smp + smr)) * _a;

            var nowa = 1 / (1 + w);
            var nowb = 0f;
            for (int i = 0; i < 50; i++)
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

            var Q = new Vector3(-rl0, 1, -rl2);
            Q /= (Q.x + Q.y + Q.z);
            Q = Q.x * P + Q.y * L + Q.z * R;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(Q, 0.1f);

            var tt = (2 * w * rl2 + rl1) / (2 * w * (rl0 + rl2 + 2 * Mathf.Sqrt(rl0 * rl2)));
            Handles.Label(L, tt.ToString());


            Vector2 prev = (Vector2)P;
            Vector2 prevr = prev;
            for (int i = 1; i <= stepPerSegment; i++)
            {
                var t = (float)i / stepPerSegment;
                var next = Calc(P,Q,R,t, w);
                var nextr = Calc(P,Q,R,t, -w);
                Gizmos.color = i%2==0 ? Color.green : Color.blue;
                Gizmos.DrawLine(prev, next);
                Gizmos.color = new Color(1, 1, 1, 0.2f);
                Gizmos.DrawLine(prevr, nextr);

                prev = next;
                prevr = nextr;
            }
        }

        [CustomEditor(typeof(RBezierTest2))]
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