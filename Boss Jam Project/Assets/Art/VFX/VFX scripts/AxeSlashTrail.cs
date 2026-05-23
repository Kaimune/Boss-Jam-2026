using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AxeSlashTrail : MonoBehaviour
{
    [Header("References")]
    public Transform top;
    public Transform bottom;

    [Header("Trail Settings")]
    public bool emitting;
    public float lifetime = 0.18f;
    public float minDistance = 0.05f;
    public int maxPoints = 24;

    [Header("Animation")]
    public float steppedFPS = 4f;
    public float textureScrollSpeed = 2f;
    public float streakStart = 1f;
    public float streakEnd = 5f;

    [Header("Material")]
    public Material trailMaterial;

    private Mesh mesh;

    private struct TrailPoint
    {
        public Vector3 top;
        public Vector3 bottom;
        public float time;
    }

    private readonly List<TrailPoint> points = new();

    private Vector3 lastTop;
    private Vector3 lastBottom;
    private bool hasLastPoint;

    private void Awake()
    {
        mesh = new Mesh();
        mesh.name = "Axe Slash Trail Mesh";
        GetComponent<MeshFilter>().mesh = mesh;

        if (trailMaterial != null)
            GetComponent<MeshRenderer>().material = trailMaterial;
    }

    private void LateUpdate()
    {
        if (top == null || bottom == null)
            return;

        float now = Time.time;

        if (emitting)
        {
            Vector3 currentTop = top.position;
            Vector3 currentBottom = bottom.position;

            bool shouldAdd =
                !hasLastPoint ||
                Vector3.Distance(currentTop, lastTop) > minDistance ||
                Vector3.Distance(currentBottom, lastBottom) > minDistance;

            if (shouldAdd)
            {
                points.Add(new TrailPoint
                {
                    top = currentTop,
                    bottom = currentBottom,
                    time = now
                });

                lastTop = currentTop;
                lastBottom = currentBottom;
                hasLastPoint = true;
            }
        }

        for (int i = points.Count - 1; i >= 0; i--)
        {
            if (now - points[i].time > lifetime)
                points.RemoveAt(i);
        }

        while (points.Count > maxPoints)
            points.RemoveAt(0);

        BuildMesh(now);

        if (!emitting && points.Count == 0)
            hasLastPoint = false;
    }

    private void BuildMesh(float now)
    {
        mesh.Clear();

        if (points.Count < 2)
            return;

        Vector3[] vertices = new Vector3[points.Count * 2];
        Vector2[] uvs = new Vector2[points.Count * 2];
        Color[] colors = new Color[points.Count * 2];
        int[] triangles = new int[(points.Count - 1) * 6];

        float trailAge01 = Mathf.Clamp01((now - points[0].time) / lifetime);

        // stepped animation time
        float steppedTime = Mathf.Floor(now * steppedFPS) / steppedFPS;

        // texture becomes streakier as the trail ages
        float streakAmount = Mathf.Lerp(streakStart, streakEnd, trailAge01);
        float scroll = steppedTime * textureScrollSpeed;

        for (int i = 0; i < points.Count; i++)
        {
            float age = now - points[i].time;
            float fade = Mathf.Clamp01(1f - age / lifetime);
            float progress = i / (float)(points.Count - 1);

            vertices[i * 2] = transform.InverseTransformPoint(points[i].top);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(points[i].bottom);

            float u = progress * streakAmount + scroll;

            uvs[i * 2] = new Vector2(u, 1);
            uvs[i * 2 + 1] = new Vector2(u, 0);

            colors[i * 2] = new Color(1, 1, 1, fade);
            colors[i * 2 + 1] = new Color(1, 1, 1, fade);
        }

        int tri = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            int a = i * 2;
            int b = i * 2 + 1;
            int c = i * 2 + 2;
            int d = i * 2 + 3;

            triangles[tri++] = a;
            triangles[tri++] = c;
            triangles[tri++] = b;

            triangles[tri++] = c;
            triangles[tri++] = d;
            triangles[tri++] = b;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    public void StartTrail()
    {
        emitting = true;
        points.Clear();
        hasLastPoint = false;
    }

    public void StopTrail()
    {
        emitting = false;
    }
}