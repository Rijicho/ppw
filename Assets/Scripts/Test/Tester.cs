using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RUtil.Curve;
using RUtil.Graphics;
using Math = System.Math;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CurveUtility
{
    public class Tester
    {
        [MenuItem("Tools/Color")]
        static void TestColor()
        {
            var tex = new Texture2D(512, 512);
            for(int i=0; i<512; i++)
            {
                for(int j=0; j<512; j++)
                {
                    var psi = (i - 256f) / 256 * PPWCurve.PsiInfinity;
                    var phi = j / 512f * 6;
                    tex.SetPixel(i, j, DrawingCanvas.PsiPhiColor3(psi, phi));
                }
            }
            tex.Apply();
            var png = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes($"{Application.dataPath}/Results/PsiPhiColor.png", png);
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Clock")]
        static void TestClock()
        {
            Debug.Log(System.Diagnostics.Stopwatch.IsHighResolution);
            Debug.Log(System.Diagnostics.Stopwatch.Frequency);
            long nanosecPerTick = (1000L * 1000L * 1000L) / System.Diagnostics.Stopwatch.Frequency;
            Debug.Log($"{nanosecPerTick}ns/tick");
        }

        [MenuItem("Tools/TestApproxTime")]
        static async UniTask TestApproxTime()
        {
            int n = 1000000;
            var sw = new System.Diagnostics.Stopwatch();
            float t;
            var init = BezierCurve.CalcQNewtonInit.Heulistic1;
            foreach(var solver in new[] { BezierCurve.CalcQMethod.Analytical_Yan, BezierCurve.CalcQMethod.Analytical_Rijicho, BezierCurve.CalcQMethod.Approximated, BezierCurve.CalcQMethod.Newton })
            {
                sw.Start();
                int i = n;
                while (i-->0)
                {
                    var P = new Vector3(Random.value, Random.value);
                    var L = new Vector3(Random.value, Random.value);
                    var R = new Vector3(Random.value, Random.value);
                    var w = Mathf.Pow(10, Random.value * 4 - 2);
                    (_, t) = BezierCurve.CalcQFromMaxCurvaturePoint(P, L, R, w, solver, init);
                }
                Debug.Log($"{solver}:{(double)sw.ElapsedTicks/n}");
                sw.Reset();
                await UniTask.Yield();
            }

        }


        [MenuItem("Tools/TestError")]
        static void TestError()
        {
            /*---------------config---------------*/
            int nx = 256;
            int ny = 256;
            //var P = new Vector3(Random.value, Random.value, 1);
            //var R = new Vector3(Random.value, Random.value, 1);
            var P = new Vector3(0.15f, 0.5f);
            var R = new Vector3(0.85f, 0.5f);
            var ws = Enumerable.Range(-8, 16).Select(x => Mathf.Pow(10, x / 4f)).ToArray();
            var method = BezierCurve.CalcQMethod.Approximated;
            var dataPath = Application.dataPath;
            /*---------------config---------------*/

            var (errors, errorPlot) = DoTestError(nx, ny, P, R, ws, method);
            //var maxerror = errors.Max();
            var sb = new System.Text.StringBuilder();
            for (int wi = 0; wi < ws.Length; wi++)
            {
                var w = ws[wi];
                var es = errors.Skip(nx * ny * wi).Take(nx * ny).OrderBy(x => x);
                var avg = es.Mean();

                var fn = es.FiveNumberSummary();
                var variance = es.Variance();
                var sigma = es.StandardDeviation();

                sb.Clear();
                sb.AppendLine($"w  : {w}");
                sb.AppendLine($"q0 : {fn[0]}");
                sb.AppendLine($"q1 : {fn[1]}");
                sb.AppendLine($"q2 : {fn[2]}");
                sb.AppendLine($"q3 : {fn[3]}");
                sb.AppendLine($"q4 : {fn[4]}");
                sb.AppendLine($"avg: {avg}");
                sb.AppendLine($"var: {variance}");
                sb.AppendLine($"std: {sigma}");
                Debug.Log(sb.ToString());
            }

            var tex = new Texture2D(nx, ny);
            for (int wi = 0; wi < ws.Length; wi++)
            {
                float m = 0;
                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < ny; j++)
                    {
                        if (m < errorPlot[wi, i, j])
                            m = errorPlot[wi, i, j];
                        var L = new Vector3((float)i / nx, (float)j / ny);
                        var p = P - L;
                        var r = R - L;
                        var inside = Vector3.Dot(p, r) + Vector3.Dot(p, p) < 0 || Vector3.Dot(p, r) + Vector3.Dot(r, r) < 0;
                        tex.SetPixel(i, j, ColorUtil.HSV(0, 0, Mathf.Clamp01(1 - errorPlot[wi, i, j]*50)));
                    }
                }
                tex.Apply();
                var png = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes($"{dataPath}/Results/NewtonFractal/error{ws[wi]}.png", png);

                Debug.Log($"errormax of w={ws[wi]}: {m}");
            }
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/TestErrorRandom")]
        static void TestErrorRandom()
        {
            /*---------------config---------------*/
            int n = 128;
            int nx = 128;
            int ny = 128;
            var ws = Enumerable.Range(-8, 16).Select(x => Mathf.Pow(10, x / 4f)).ToArray();
            var method = BezierCurve.CalcQMethod.Approximated;
            var dataPath = Application.dataPath;
            /*---------------config---------------*/

            var errors = new List<float>();
            var bigerrors = new List<(Vector3 P, Vector3 R, float w, float error)>();
            var lockobj = new object();
            var rand = new System.Random();
            string errorlog = "";
            Parallel.For(0, n, i =>
            {
                float px, rx;//, py, ry;
                lock (lockobj)
                {
                    px = rand.Next(4000) / 10000f;
                    //py = rand.Next(10000) / 10000f;
                    rx = rand.Next(6000,10000) / 10000f;
                    //ry = rand.Next(10000) / 10000f;
                }
                var P = new Vector3(px, 0.5f);
                var R = new Vector3(rx, 0.5f);
                //var P = new Vector3(0.25f, 0.4f);
                //var R = new Vector3(0.75f, 0.6f);
                var (e, _) = DoTestError(nx, ny, P, R, ws, method);
                lock (lockobj)
                {
                    errors.AddRange(e);
                }
            });

            errors.Sort();
            var sb = new System.Text.StringBuilder();

            var avg = errors.Mean();

            var fn = errors.FiveNumberSummary();
            var variance = errors.Variance();
            var sigma = errors.StandardDeviation();


            sb.AppendLine($"q0 : {fn[0]}");
            sb.AppendLine($"q1 : {fn[1]}");
            sb.AppendLine($"q2 : {fn[2]}");
            sb.AppendLine($"q3 : {fn[3]}");
            sb.AppendLine($"q4 : {fn[4]}");
            sb.AppendLine($"avg: {avg}");
            sb.AppendLine($"var: {variance}");
            sb.AppendLine($"std: {sigma}");

            sb.AppendLine("big errors:");
            foreach(var be in bigerrors)
            {
                sb.AppendLine($"(P,R)=(({be.P.x},{be.P.y}),({be.R.x},{be.R.y})), w={be.w}, max={be.error}");
            }
            if (!string.IsNullOrWhiteSpace(errorlog))
            {
                sb.AppendLine();
                sb.AppendLine(errorlog);
            }
            Debug.Log(sb.ToString());
        }


        static (List<float> errors, float[,,] errorPlot) DoTestError(int nx, int ny, Vector3 P, Vector3 R, float[] ws, BezierCurve.CalcQMethod method)
        {
            var errors = new List<float>(ws.Length * nx * ny);
            while (errors.Count < ws.Length * nx * ny)
                errors.Add(default);
            var errorPlot = new float[ws.Length, nx, ny];
            var lockobj = new object();


            Parallel.For(0, ws.Length, wi =>
            {
                var w = ws[wi];
                for (int x = 0; x < nx; x++)
                {
                    for (int y = 0; y < ny; y++)
                    {
                        var L = new Vector3((float)x / nx, (float)y / nx);
                        var (qOptimal,tOptimal) = BezierCurve.CalcQFromMaxCurvaturePoint(P, L, R, w, BezierCurve.CalcQMethod.Analytical_Rijicho);
                        var (qApprox, tApprox) = BezierCurve.CalcQFromMaxCurvaturePoint(P, L, R, w, method, BezierCurve.CalcQNewtonInit.Heulistic1);

                        var lOptimal = L;// BezierCurve.PlotRationalBezier(P, qOptimal, R, w, tOptimal);
                        var lApprox = BezierCurve.PlotRationalBezier(P, qApprox, R, w, tApprox);

                        if(qApprox == L && (tApprox == 0 || tApprox == 1))
                        {
                            errors.Add(0);
                            errorPlot[wi, x, y] = 0;
                            continue;
                        }

                        var error = Mathf.Abs(tOptimal - tApprox);
                        //var error = Vector3.Distance(qOptimal, qApprox);
                        //var error = Vector3.Distance(lOptimal, lApprox);

                        lock (lockobj)
                        {
                            errors[wi*nx*ny+nx*y+x] = error;
                            errorPlot[wi, x, y] = error;
                        }
                    }
                }

            });

            return (errors, errorPlot);


        }

        [MenuItem("Tools/TestNewton")]
        static void TestNewton()
        {
            /*---------------config---------------*/
            int nx = 256;
            int ny = 256;
            //var P = new Vector3(Random.value, Random.value, 1);
            //var R = new Vector3(Random.value, Random.value, 1);
            var P = new Vector3(0.25f, 0.4f, 1);
            var R = new Vector3(0.75f, 0.6f, 1);
            var ws = new[] { 0.5f, 2f };
            //var ws = Enumerable.Range(1, 500).Select(x => x / 100f);
            //var ws = Enumerable.Range(1, 10).Select(x => x / 10000f);
            //var ws = new[] { 0.0001f, 0.001f, 0.01f, 0.05f, 0.1f, 0.3f, 0.5f, 0.7f, 0.9f, 1f, 1.5f, 2f, 3f, 4f, 5f, 10f, 100f, 1000f, 10000f };
            var qsolver = BezierCurve.CalcQMethod.Newton;
            var initmethod = BezierCurve.CalcQNewtonInit.Heulistic1;
            var epsilon = 0.001;
            var jetRoundFactor = 1000f;
            var newtonIterationMax = 10;
            /*---------------config---------------*/




            bool isAnalytic = qsolver == BezierCurve.CalcQMethod.Analytical_Rijicho || qsolver == BezierCurve.CalcQMethod.Analytical_Yan;

            var dataPath = Application.dataPath;


            var allresults = new Dictionary<float, (Vector3 p, Vector3 r, float w, double a, double b, int type, int iter)[,]>();
            foreach (var w in ws)
            {
                allresults[w] = new (Vector3 p, Vector3 r, float w, double a, double b, int type, int iter)[nx, ny];
            }
            Parallel.ForEach(ws, w =>
            {
                var results = allresults[w];
                List<int> iterCounts = new List<int>();
                for (int x = 0; x < nx; x++)
                {
                    for (int y = 0; y < ny; y++)
                    {
                        var L = new Vector3((float)x / nx, (float)y / nx, 1);

                        if (P == R) { results[x, y] = (default, default, w, 0, 0, -2, 0); continue; }
                        if (L == P) { results[x, y] = (default, default, w, 1, 1, -2, 0); continue; }
                        if (L == R) { results[x, y] = (default, default, w, 1, -1, -2, 0); continue; }


                        var (p, r) = (P - L, R - L);
                        var (mp, mr) = (p.magnitude, r.magnitude);
                        var (smp, smr) = (p.sqrMagnitude, r.sqrMagnitude);
                        var (smppr, smpmr) = ((p + r).sqrMagnitude, (p - r).sqrMagnitude);
                        var pr = Vector3.Dot(p, r);
                        var ww = w * w;
                        var isoptimal = false;
                        double nowad, nowbd;

                        if (isAnalytic)
                        {
                            (nowad, nowbd) = BezierCurve.InitializeNewtonForCalcQAnalytical(P, L, r, w, mp, mr);
                            isoptimal = true;
                        }
                        else
                        {
                            (nowad, nowbd, isoptimal) = BezierCurve.InitializeNewtonForCalcQ(initmethod, P, L, R, w, mp, mr, smp, smr, smppr, smpmr, pr);
                        }



                        var (nowa, nowb) = (nowad, nowbd);

                        if (double.IsNaN(nowa) || double.IsNaN(nowb))
                        {
                            results[x, y] = (p, r, w, nowa, nowb, -3, 0);
                            continue;
                        }


                        if (isoptimal)
                        {
                            results[x, y] = (p, r, w, nowa, nowb, 0, 0);
                            continue;
                        }




                        double f(double _a, double _b) => (1 - ww) * _a * _a - 2 * _a + 1 + ww * _b * _b;
                        double g(double _a, double _b) =>
                            smppr * _b * _b * _b
                            - (smp - smr) * (1 - 2 * _a) * _b * _b
                            + (smpmr * _a - 2 * (smp + smr)) * _a * _b
                            - (smp - smr) * _a * _a;
                        double fda(double _a) => 2 * (1 - ww) * _a - 2;
                        double fdb(double _b) => 2 * ww * _b;
                        double gda(double _a, double _b) => 2 * (smp - smr) * _b * _b + 2 * _b * smpmr * _a - 2 * _b * (smp + smr) - 2 * (smp - smr) * _a;
                        double gdb(double _a, double _b) => 3 * smppr * _b * _b - 2 * (smp - smr) * (1 - 2 * _a) * _b + (smpmr * _a - 2 * (smp + smr)) * _a;



                        int j = 0;
                        for (; j < newtonIterationMax; j++)
                        {
                            var rf = f(nowa, nowb);
                            var rg = g(nowa, nowb);
                            if (Math.Abs(rf) <= epsilon && Math.Abs(rg) <= epsilon)
                            {
                                j++;
                                break;
                            }
                            var rfda = fda(nowa);
                            var rfdb = fdb(nowb);
                            var rgda = gda(nowa, nowb);
                            var rgdb = gdb(nowa, nowb);
                            var dby = rfda * rgdb - rfdb * rgda;

                            nowa -= (rgdb * rf - rfdb * rg) / dby;
                            nowb -= (-rgda * rf + rfda * rg) / dby;
                        }

                        iterCounts.Add(j);

                        int type = 0;

                        if (double.IsNaN(nowa) || double.IsNaN(nowb))
                        {
                            type = -3;
                        }
                        else if (Math.Abs(f(nowa, nowb)) > epsilon || Math.Abs(g(nowa, nowb)) > epsilon)
                        {
                            type = -4;
                        }
                        else if (nowa < 0)
                        {
                            type = 2;
                        }
                        else if (nowa > 1)
                        {
                            type = 3;
                        }

                        else if (Math.Abs(nowa - 1) < epsilon && Math.Abs(nowb - 1) < epsilon)
                        {
                            type = 1;
                        }
                        else if (Math.Abs(nowa - 1) < epsilon && Math.Abs(nowb + 1) < epsilon)
                        {
                            type = -1;
                        }

                        results[x, y] = (p, r, w, nowa, nowb, type, j);
                    }

                }




                if (iterCounts.Count > 0)
                {
                    var avg = iterCounts.Average();
                    iterCounts.Sort();
                    var med = iterCounts[iterCounts.Count / 2];
                    int mode = -1;
                    int max = 0;
                    int prev = -1;
                    int currentcnt = 0;
                    foreach (var c in iterCounts)
                    {
                        if (prev != c)
                        {
                            if (max < currentcnt)
                            {
                                mode = prev;
                                max = currentcnt;
                            }
                            currentcnt = 0;
                        }
                        currentcnt++;
                        prev = c;
                    }
                    Debug.Log($"Iteration for w={w}: avg={avg}, median={med}, mode={mode}");
                }


            });

            foreach (var w in ws)
            {
                var tex = new Texture2D(nx, ny);
                var tex2 = new Texture2D(nx, ny);
                var results = allresults[w];
                for (int x = 0; x < nx; x++)
                {
                    for (int y = 0; y < ny; y++)
                    {
                        Color c = new Color(0, Mathf.Clamp01(1 - results[x, y].iter / 10f), 0);

                        //if (w < -Mathf.Cos(Vector3.Angle(results[x, y].p, results[x, y].r) * Mathf.Deg2Rad))
                        //    c = new Color(0.9f,0.9f,0.9f);

                        switch (results[x, y].type)
                        {
                            case -4: c = Color.black; break;    //未収束
                            case -3: c = Color.gray; break;     //NaN
                            case -2: c = Color.white; break;    //解析解
                            case -1: c = Color.blue; break;     //(1,-1)
                            case 1: c = Color.red; break;       //(1,1)
                            case 2: c = Color.yellow; break;    //a<0
                            case 3: c = Color.cyan; break;   //a>1
                            default: //正常
                                c = new Color(0, Mathf.Clamp01(1 - results[x, y].iter / (float)newtonIterationMax), 0);
                                break;
                        }
                        tex.SetPixel(x, y, c);

                        /* GrayScale */
                        //c = Color.white * Mathf.Abs(results[x, y].b);
                        //c.a = 1;

                        /*Jet*/
                        //*
                        {
                            c = ColorUtil.Jet(Mathf.Round((float)results[x, y].b * jetRoundFactor) / jetRoundFactor, -1, 1);
                        }
                        //*/

                        /*Jet - ts[i]*/
                        /*
                        {
                            double rl0, rl1, rl2;
                            var a = results[x, y].a;
                            var b = results[x, y].b;
                            rl0 = (a + b) / 2;
                            rl2 = (a - b) / 2;
                            rl1 = 1 - rl0 - rl2;

                            //rl0=0, rl2=0の付近で誤差により積が負になるので調整
                            var sqrt = rl0 * rl2 < 0 ? 0 : System.Math.Sqrt(rl0 * rl2);

                            var ti = ((2 * w * rl2 + rl1) / (2 * w * (rl0 + rl2 + 2 * sqrt)));

                            ti = ti < 0 ? 0 : ti > 1 ? 1 : ti;

                            c = GraphicMgr.ToJet((float)(System.Math.Round(ti * jetRoundFactor) / jetRoundFactor), 0, 1);
                        }
                        //*/

                        tex2.SetPixel(x, y, c);

                    }
                }

                //*
                var pxp = new Vector2Int(Mathf.RoundToInt(P.x * nx), Mathf.RoundToInt(P.y * ny));
                var pxr = new Vector2Int(Mathf.RoundToInt(R.x * nx), Mathf.RoundToInt(R.y * ny));
                {
                    foreach (var t in new[] { tex, tex2 })
                    {
                        t.SetPixel(pxp.x, pxp.y, Color.white);
                        t.SetPixel(pxp.x + 1, pxp.y, Color.white);
                        t.SetPixel(pxp.x - 1, pxp.y, Color.white);
                        t.SetPixel(pxp.x, pxp.y + 1, Color.white);
                        t.SetPixel(pxp.x, pxp.y - 1, Color.white);
                        t.SetPixel(pxp.x + 1, pxp.y + 1, Color.white);
                        t.SetPixel(pxp.x - 1, pxp.y + 1, Color.white);
                        t.SetPixel(pxp.x + 1, pxp.y - 1, Color.white);
                        t.SetPixel(pxp.x - 1, pxp.y - 1, Color.white);

                        t.SetPixel(pxr.x, pxr.y, Color.white);
                        t.SetPixel(pxr.x + 1, pxr.y, Color.white);
                        t.SetPixel(pxr.x - 1, pxr.y, Color.white);
                        t.SetPixel(pxr.x, pxr.y + 1, Color.white);
                        t.SetPixel(pxr.x, pxr.y - 1, Color.white);
                        t.SetPixel(pxr.x + 1, pxr.y + 1, Color.white);
                        t.SetPixel(pxr.x - 1, pxr.y + 1, Color.white);
                        t.SetPixel(pxr.x + 1, pxr.y - 1, Color.white);
                        t.SetPixel(pxr.x - 1, pxr.y - 1, Color.white);

                        for (int i = pxp.x; i <= pxr.x; i++)
                        {
                            t.SetPixel(i, Mathf.RoundToInt(pxp.y + (float)(i - pxp.x) * (pxr.y - pxp.y) / (pxr.x - pxp.x)), Color.white);
                        }


                        t.Apply();
                    }
                }
                //*/



                var png = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes($"{dataPath}/Results/NewtonFractal/cs_w{w:0.00000}{(isAnalytic ? "a" : "n")}.png", png);

                var png2 = tex2.EncodeToPNG();
                System.IO.File.WriteAllBytes($"{dataPath}/Results/NewtonFractal/ct_w{w:0.00000}{(isAnalytic ? "a" : "n")}.png", png2);
            }

            AssetDatabase.Refresh();

            Debug.Log("finish.");

        }


        [MenuItem("Tools/TestEquations")]
        static void TestEquations()
        {
            var qsolver = BezierCurve.CalcQMethod.Newton;
            var qinit = BezierCurve.CalcQNewtonInit.Heulistic1;
            bool showColorCircle = false;


            string now = System.DateTime.Now.ToString("HHmmss");

            int n = 2048;

            var p = new Vector3(-1f, 0f, 0);
            var r = new Vector3(-0.2f, 2.7f, 0);

            //var p = new Vector3(Random.value, Random.value, 1) * 10;
            //var r = new Vector3(Random.value, Random.value, 1) * 10;
            var (mp, mr) = (p.magnitude, r.magnitude);
            var (smp, smr) = (p.sqrMagnitude, r.sqrMagnitude);
            var (smppr, smpmr) = ((p + r).sqrMagnitude, (p - r).sqrMagnitude);
            var pr = Vector3.Dot(p, r);


            //foreach (var w in new[] { 0.0001f, 0.001f, 0.01f, 0.05f, 0.1f, 0.3f, 0.5f, 0.7f, 0.9f, 1f, 1.5f, 2f, 5f, 10f, 100f })
            //foreach (var w in new[] { 0.0001f, 0.001f, 0.01f, 0.1f, 0.5f, 1f, 2f, 3f, 4f })
            foreach (var w in new[] { 0.1f, 2f, 0.75f })
            {
                var ww = w * w;

                var tex = new Texture2D(n, n);
                var gtex = new Texture2D(n, n);
                var vtex = new Texture2D(n, n);

                var vvalues = new Vector2[n, n];
                var values = new float[n, n];
                var pixels = new Color[n, n];
                var vpixels = new Color[n, n];
                var gpixels = new Color[n, n];


                float f(float _a, float _b) => (1 - ww) * _a * _a - 2 * _a + 1 + ww * _b * _b;
                float g(float _a, float _b) =>
                    smppr * _b * _b * _b
                    - (smp - smr) * (1 - 2 * _a) * _b * _b
                    + (smpmr * _a - 2 * (smp + smr)) * _a * _b
                    - (smp - smr) * _a * _a;
                float fda(float _a) => 2 * (1 - ww) * _a - 2;
                float fdb(float _b) => 2 * ww * _b;
                float gda(float _a, float _b) => 2 * (smp - smr) * _b * _b + 2 * _b * smpmr * _a - 2 * _b * (smp + smr) - 2 * (smp - smr) * _a;
                float gdb(float _a, float _b) => 3 * smppr * _b * _b - 2 * (smp - smr) * (1 - 2 * _a) * _b + (smpmr * _a - 2 * (smp + smr)) * _a;

                /*
                for (int x = 0; x < n; x++)
                {
                    for (int y = 0; y < n; y++)
                    {
                        //-2～2で描画
                        var a = ((float)x / n - 0.5f) * 4;
                        var b = ((float)y / n - 0.5f) * 4;

                        var rf = f(a, b);
                        var rg = g(a, b);
                        var rn = Mathf.Sqrt(rf * rf + rg * rg);
                        vvalues[x, y] = new Vector2(rf, rg);
                        values[x, y] = rn;

                        Color c = GraphicMgr.ToJet(rn, 0, 5);

                        if (Mathf.Abs(rn) < 0.08f)
                        {
                            c = Color.magenta;
                        }
                        else if(Mathf.Approximately(a,b) || Mathf.Approximately(a,-b) || Mathf.Approximately(a, 1))
                        {
                            c = new Color(c.r / 2, c.g / 2, c.b / 2);
                        }
                        else if (Mathf.Approximately(a,0) || Mathf.Approximately(b,0))
                        {
                            c = new Color(c.r / 2, c.g / 2, c.b / 2);
                        }
                        pixels[x, y] = c;
                    }
                }
                */

                /*
                for(int x = 0; x<n-1; x++)
                {
                    for(int y=0;y<n-1; y++)
                    {
                        var h = 4f / n;
                        var deltaX = values[x + 1, y] - values[x, y];
                        var deltaY = values[x, y + 1] - values[x, y];


                        var u = new Vector3(h, 0, deltaX);
                        var v = new Vector3(0, h, deltaY);
                        var grad = Vector3.Cross(u, v).normalized;

                        var c = new Color(0.5f + grad.x / 2, 0.5f + grad.y / 2, 0.5f + grad.z);


                        var a = ((float)x / n - 0.5f) * 4;
                        var b = ((float)y / n - 0.5f) * 4;

                        if (Mathf.Approximately(a, b) || Mathf.Approximately(a, -b) || Mathf.Approximately(a, 1))
                        {
                            c = new Color(c.r / 2, c.g / 2, c.b / 2);
                        }
                        else if (Mathf.Approximately(a, 0) || Mathf.Approximately(b, 0))
                        {
                            c = new Color(c.r / 2, c.g / 2, c.b / 2);
                        }

                        gpixels[x, y] = c;
                    }
                }
                */

                for (int x = 0; x < n - 1; x++)
                {
                    for (int y = 0; y < n - 1; y++)
                    {

                        var a = ((float)x / n - 0.5f) * 4;
                        var b = ((float)y / n - 0.5f) * 4;

                        var lmax = 80;
                        var lmin = 10;

                        if (showColorCircle)
                        {
                            var center = new Vector2(-1.4f, 1.4f);
                            var r2 = (a - center.x) * (a - center.x) + (b - center.y) * (b - center.y);

                            if (r2 < 0.16f)
                            {
                                /*
                                vpixels[x, y] = GraphicMgr.FromLab(
                                    lmax, 
                                    lmax/100f*Mathf.Clamp((a - center.x)/0.2f * 100, -86,98),
                                    lmax/100f*Mathf.Clamp((b - center.y)/0.2f * 100, -107,94));
                                */
                                vpixels[x, y] = ColorUtil.HSV(
                                    Mathf.Atan2(b - center.y, a - center.x) * Mathf.Rad2Deg / 360f,
                                    Mathf.Clamp01(Mathf.Sqrt(r2) / 0.4f),
                                    1);

                                continue;
                            }
                            else if (r2 < 0.20f)
                            {
                                vpixels[x, y] = Color.black;
                                continue;
                            }
                        }

                        var rf = f(a, b);
                        var rg = g(a, b);
                        var rfda = fda(a);
                        var rfdb = fdb(b);
                        var rgda = gda(a, b);
                        var rgdb = gdb(a, b);
                        var dby = rfda * rgdb - rfdb * rgda;
                        var grad = new Vector2();
                        grad.x = -(rgdb * rf - rfdb * rg) / dby;
                        grad.y = -(-rgda * rf + rfda * rg) / dby;

                        var m = Mathf.Clamp(Mathf.Clamp01(grad.magnitude * 30) * 100, lmin, lmax);
                        //var c = GraphicMgr.FromLab(m, Mathf.Clamp(grad.x * 200,-86,98)*m/100f, Mathf.Clamp(grad.y * 200,-107,94)*m/100f);

                        var c = ColorUtil.HSV(Mathf.Atan2(grad.y, grad.x) * Mathf.Rad2Deg / 360f, Mathf.Clamp(grad.magnitude * 2, 0, 1f), 1); //Mathf.Clamp01(grad.magnitude * 30));



                        //var c = new Color(m * (0.5f + grad.x / 2), m * (0.5f + grad.y / 2), 0);

                        var lum = 0.8f;

                        if (Mathf.Approximately(a, b) || Mathf.Approximately(a, -b) || Mathf.Approximately(a, 1))
                        {
                            c *= lum; c.a = 1;
                        }
                        else if (Mathf.Approximately(a, 0) || Mathf.Approximately(b, 0))
                        {
                            c *= lum; c.a = 1;
                        }

                        vpixels[x, y] = c;
                    }
                }

                double nowad, nowbd;
                bool isoptimal;

                if(qsolver == BezierCurve.CalcQMethod.Newton)
                {
                    (nowad, nowbd, isoptimal) = BezierCurve.InitializeNewtonForCalcQ(qinit, p, Vector3.zero, r, w, mp, mr, smp, smr, smppr, smpmr, pr, true);
                }
                else
                {
                    (nowad, nowbd) = BezierCurve.InitializeNewtonForCalcQAnalytical(p, Vector3.zero, r, w, mp, mr);
                    isoptimal = true;
                }


                //var (nowad, nowbd) = KConics.SolveStep3(p, Vector3.zero, r, w);
                //var isoptimal = true;

                var (nowa, nowb) = ((float)nowad, (float)nowbd);

                if (!isoptimal)
                {
                    Color linecol = Color.black;
                    if (float.IsNaN(nowa) || float.IsNaN(nowb))
                    {
                        (nowa, nowb) = (1 / 1 + w, 0);
                        linecol = Color.red;
                    }

                    float epsilon = 0.0001f;
                    for (int j = 0; j < 30; j++)
                    {
                        var rf = f(nowa, nowb);
                        var rg = g(nowa, nowb);
                        if (Mathf.Abs(rf) < epsilon && Mathf.Abs(rg) < epsilon)
                        {
                            j++;
                            break;
                        }
                        var rfda = fda(nowa);
                        var rfdb = fdb(nowb);
                        var rgda = gda(nowa, nowb);
                        var rgdb = gdb(nowa, nowb);
                        var dby = rfda * rgdb - rfdb * rgda;

                        var (preva, prevb) = (nowa, nowb);

                        nowa -= (rgdb * rf - rfdb * rg) / dby;
                        nowb -= (-rgda * rf + rfda * rg) / dby;

                        Vector2Int from = new Vector2Int(Mathf.RoundToInt((preva + 2) / 4 * n), Mathf.RoundToInt((prevb + 2) / 4 * n));
                        Vector2Int to = new Vector2Int(Mathf.RoundToInt((nowa + 2) / 4 * n), Mathf.RoundToInt((nowb + 2) / 4 * n));

                        for (int k = -1; k <= 1; k++)
                        {
                            for (int l = -1; l <= 1; l++)
                            {
                                vpixels[from.x + k, from.y + l] = linecol;
                                vpixels[to.x + k, to.y + l] = linecol;
                            }
                        }


                        var dydx = (float)(to.y - from.y) / (to.x - from.x);
                        if (Mathf.Abs(dydx) < 1)
                        {
                            if (from.x > to.x)
                            {
                                var tmp = from;
                                from = to;
                                to = tmp;
                            }
                            for (int x = from.x; x < to.x; x++)
                            {
                                var y = Mathf.RoundToInt(from.y + dydx * (x - from.x));
                                vpixels[x, y] = linecol;
                            }
                        }
                        else
                        {
                            if (from.y > to.y)
                            {
                                var tmp = from;
                                from = to;
                                to = tmp;
                            }

                            for (int y = from.y; y < to.y; y++)
                            {
                                var x = Mathf.RoundToInt(from.x + 1 / dydx * (y - from.y));
                                vpixels[x, y] = linecol;
                            }
                        }

                    }

                }

                Vector2Int result = new Vector2Int(Mathf.RoundToInt((nowa + 2) / 4 * n), Mathf.RoundToInt((nowb + 2) / 4 * n));
                for (int k = -1; k <= 1; k++)
                {
                    for (int l = -1; l <= 1; l++)
                    {
                        vpixels[result.x + k, result.y + l] = Color.black;
                    }
                }


                for (int x = 0; x < n; x++)
                {
                    for (int y = 0; y < n; y++)
                    {
                        //tex.SetPixel(x, y, pixels[x, y]);
                        vtex.SetPixel(x, y, vpixels[x, y]);
                        /*
                        if (x < n - 1 && y < n - 1)
                        {
                            gtex.SetPixel(x, y, gpixels[x, y]);
                        }
                        else if (x == n - 1 && y == n - 1)
                        {
                            gtex.SetPixel(x, y, gpixels[x - 1, y - 1]);
                        }
                        else if(x==n-1)
                        {
                            gtex.SetPixel(x, y, gpixels[x - 1, y]);
                        }
                        else if (y == n - 1)
                        {
                            gtex.SetPixel(x, y, gpixels[x, y-1]);
                        }
                        */
                    }
                }



                //tex.Apply();
                //gtex.Apply();
                vtex.Apply();

                //var png = tex.EncodeToPNG();
                //var gpng = gtex.EncodeToPNG();
                var vpng = vtex.EncodeToPNG();

                var dir = $"{Application.dataPath}/Results/fg_p{p}_r{r}";
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                //System.IO.File.WriteAllBytes($"{dir}/w{w:0.00000}n.png", png);
                //System.IO.File.WriteAllBytes($"{dir}/w{w:0.00000}g.png", gpng);
                System.IO.File.WriteAllBytes($"{dir}/w{w:0.00000}v.png", vpng);

            }
            AssetDatabase.Refresh();
        }
    }
}