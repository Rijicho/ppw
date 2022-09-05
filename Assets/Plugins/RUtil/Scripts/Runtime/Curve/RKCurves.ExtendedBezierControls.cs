namespace RUtil.Curve
{
    public static partial class RKCurves
    {
        struct ExtendedBezierControls
        {
            ControlPoint top;
            int n;
            ControlPoint[] cs;
            ControlPoint bottom;

            public ControlPoint this[int i]
            {
                get => i == 0 ? top : i <= n ? cs[i * 2 - 1] : bottom;
                set
                {
                    if (i == 0) top = value;
                    else if (i <= n) cs[i * 2 - 1] = value;
                    else bottom = value;
                }
            }

            public ExtendedBezierControls(BezierControls cs)
            {
                n = cs.SegmentCount;
                top = cs[cs.SegmentCount - 1, 1];
                this.cs = cs.Points;
                bottom = cs[0, 1];
            }
        }
    }
}
