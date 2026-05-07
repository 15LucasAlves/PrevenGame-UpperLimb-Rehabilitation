using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// OmmoGridVisualizer - Grelha 3D wireframe + marcadores dos sensores via GL.
/// Attach à GridCamera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class OmmoGridVisualizer : MonoBehaviour
{
    [Header("Grelha")]
    public float GridHalfSize  = 10f;   // metros (Unity units com scale=10 → 100cm)
    public int   GridDivisions = 10;
    public Color GridColor     = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Marcador")]
    public Color MarkerColor   = new Color(0.9f, 0.08f, 0.08f, 1f);
    public float MarkerSize    = 0.4f;

    [Header("HUD")]
    public TMPro.TextMeshProUGUI HUDTopBarText;
    public TMPro.TextMeshProUGUI HUDDeviceCountText;
    public TMPro.TextMeshProUGUI HUDBaseStationText;
    public TMPro.TextMeshProUGUI HUDDeviceInfoText;

    private Material     _mat;
    private OmmoDevice[] _devices   = new OmmoDevice[0];
    private float        _refreshTimer;

    void Awake()
    {
        var shader = Shader.Find("Hidden/Internal-Colored");
        _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        _mat.SetInt("_ZWrite",   0);
    }

    void Update()
    {
        // Refresh lista de dispositivos a cada 0.5s
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer > 0.5f)
        {
            _devices = System.Array.FindAll(FindObjectsOfType<OmmoDevice>(), d => d.gameObject.activeInHierarchy);
            _refreshTimer = 0f;
        }

        UpdateHUD();
    }

    void UpdateHUD()
    {
        if (_devices == null || _devices.Length == 0) return;

        // Recolhe posições dos sensores
        var positions = new List<Vector3>();
        foreach (var dev in _devices)
        {
            if (dev == null) continue;
            // Cada filho do OmmoDevice é um sensor
            for (int i = 0; i < dev.transform.childCount; i++)
            {
                var child = dev.transform.GetChild(i);
                if (child != null && child.position != Vector3.zero)
                    positions.Add(child.position);
            }
            // Se não tiver filhos ainda, usa a posição do próprio
            if (dev.transform.childCount == 0)
                positions.Add(dev.transform.position);
            // Remove filhos em posição exactamente zero — sem dados ainda
        }

        if (positions.Count == 0) return;

        Vector3 p = positions[0];

        if (HUDTopBarText)
            HUDTopBarText.text = $"{p.x * 10f:F2},{p.y * 10f:F2},{p.z * 10f:F2}  " +
                                 $"(UUID: {(_devices.Length > 0 ? _devices[0].name : "—")}) " +
                                 $"(Port: 0) (Sensor: 0)   Fusion Mode: Default";

        if (HUDDeviceCountText)
            HUDDeviceCountText.text = $"Device Count: {_devices.Length}";

        if (HUDDeviceInfoText)
        {
            string info = "";
            for (int i = 0; i < positions.Count && i < 4; i++)
            {
                var pos = positions[i];
                info += $"S{i} X: {pos.x * 10f:F2}  Y: {pos.y * 10f:F2}  Z: {pos.z * 10f:F2}\n";
            }
            HUDDeviceInfoText.text = info.TrimEnd();
        }
    }

    void OnRenderObject()
    {
        if (Camera.current != GetComponent<Camera>()) return;
        _mat.SetPass(0);
        GL.PushMatrix();
        DrawGrid();
        DrawMarkers();
        GL.PopMatrix();
    }

    void DrawGrid()
    {
        float s    = GridHalfSize;
        float step = (s * 2f) / GridDivisions;

        GL.Begin(GL.LINES);
        GL.Color(GridColor);

        // Piso (XZ, Y=0)
        for (int i = 0; i <= GridDivisions; i++)
        {
            float t = -s + i * step;
            GL.Vertex3(-s, 0, t); GL.Vertex3(s, 0, t);
            GL.Vertex3(t, 0, -s); GL.Vertex3(t, 0, s);
        }

        // Parede traseira (XY, Z=+s)
        for (int i = 0; i <= GridDivisions; i++)
        {
            float t = -s + i * step;
            float h = s * 0.8f;
            GL.Vertex3(-s, i * h / GridDivisions, s);
            GL.Vertex3( s, i * h / GridDivisions, s);
            GL.Vertex3(t, 0, s);
            GL.Vertex3(t, h, s);
        }

        // Parede esquerda (ZY, X=-s)
        for (int i = 0; i <= GridDivisions; i++)
        {
            float t = -s + i * step;
            float h = s * 0.8f;
            GL.Vertex3(-s, i * h / GridDivisions, -s);
            GL.Vertex3(-s, i * h / GridDivisions,  s);
            GL.Vertex3(-s, 0, t);
            GL.Vertex3(-s, h, t);
        }

        GL.End();
    }

    void DrawMarkers()
    {
        if (_devices == null) return;

        foreach (var dev in _devices)
        {
            if (dev == null) continue;

            int sensorCount = dev.transform.childCount > 0 ? dev.transform.childCount : 1;

            for (int i = 0; i < sensorCount; i++)
            {
                Vector3 pos = dev.transform.childCount > 0
                    ? dev.transform.GetChild(i).position
                    : dev.transform.position;

                DrawCrossMarker(pos, MarkerColor, MarkerSize);
            }
        }
    }

    void DrawCrossMarker(Vector3 pos, Color color, float size)
    {
        // Cruz horizontal
        GL.Begin(GL.LINES);
        GL.Color(color);
        GL.Vertex3(pos.x - size, pos.y, pos.z);
        GL.Vertex3(pos.x + size, pos.y, pos.z);
        GL.Vertex3(pos.x, pos.y, pos.z - size);
        GL.Vertex3(pos.x, pos.y, pos.z + size);
        // Linha vertical curta
        GL.Vertex3(pos.x, pos.y - size * 0.3f, pos.z);
        GL.Vertex3(pos.x, pos.y + size * 0.3f, pos.z);
        GL.End();

        // Círculo
        GL.Begin(GL.LINE_STRIP);
        GL.Color(color);
        int segs = 24;
        float r  = size * 0.5f;
        for (int i = 0; i <= segs; i++)
        {
            float a = i * Mathf.PI * 2f / segs;
            GL.Vertex3(pos.x + Mathf.Cos(a) * r, pos.y, pos.z + Mathf.Sin(a) * r);
        }
        GL.End();
    }
}
