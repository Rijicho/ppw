using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class GraphicTest : MonoBehaviour
{
    Mesh mesh;
    CommandBuffer buffer;
    Camera cam;

    public Material mat;
    public Vector3 from;
    public Vector3 to;
    public float width = 0.1f;
    public Color color = Color.green;

    public Vector3 P, Q, R;

    public Vector3 C;
    public float radius = 1;
    public float angle = 360;
    public int segmentPerCircle = 16;

    void Start()
    {
        mesh = new Mesh();
        buffer = new CommandBuffer();
        cam = Camera.main;
        cam.AddCommandBuffer(CameraEvent.AfterForwardOpaque, buffer);
    }

    void Update()
    {
        //GraphicMgr.Draw2DLine(mesh, buffer,mat, from, to, color, width);

        //GraphicMgr.Draw2DPath(mesh, buffer, mat, new List<Vector3> { P,Q,R }, color, width);

        //GraphicMgr.DrawFan(mesh, buffer, mat, C, Vector3.up, Quaternion.Euler(0, 0, -angle) * Vector3.up, radius, segmentPerCircle, color);
    }
}
