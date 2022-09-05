namespace RUtil.Curve
{
    public static partial class RKCurves
    {
        struct ExtendedPlayerControls
        {
            ControlPoint top;
            ControlPoint[] ps;
            ControlPoint bottom;

            public ControlPoint this[int i]
            {
                get => i == 0 ? top : i <= ps.Length ? ps[i - 1] : bottom;
                set
                {
                    if (i == 0) top = value;
                    else if (i <= ps.Length) ps[i - 1] = value;
                    else bottom = value;
                }
            }

            public ExtendedPlayerControls(ControlPoint[] ps, BezierControls cs)
            {
                top = cs[cs.SegmentCount - 1, 1];
                this.ps = ps;
                bottom = cs[0, 1];
            }
        }
    }
}
