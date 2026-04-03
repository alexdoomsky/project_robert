using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BattleCameraControllerV2 : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private HexGridV2 grid;

    [Header("Fit / Bounds")]
    [SerializeField] private bool fitToGridOnStart = true;
    [SerializeField] private float boundsPadding = 0.75f;

    [Tooltip("Optional: if 0, plane height will be taken from bounds center.")]
    [SerializeField] private float gridPlaneYOverride = 0f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 6f;
    [SerializeField] private float minOrthoSize = 3f;
    [SerializeField] private float maxOrthoSize = 0f; // auto if 0
    [SerializeField] private float zoomSmooth = 12f;

    [Header("Pan")]
    [SerializeField] private int panMouseButton = 2; // 2=MMB, 1=RMB
    [SerializeField] private float panSpeed = 1.0f;
    [SerializeField] private float panSmooth = 14f;

    [Header("Input")]
    [SerializeField] private bool ignoreWhenPointerOverUI = true;

    private Bounds _gridBounds;
    private bool _hasBounds;

    private Vector3 _targetPos;
    private float _targetOrtho;

    private Vector3 _dragStartWorld;
    private bool _dragging;

    private float _gridPlaneY;

    private void Awake()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (grid == null) grid = FindObjectOfType<HexGridV2>();

        _targetPos = transform.position;
        _targetOrtho = cam != null ? cam.orthographicSize : 10f;
    }

    private void OnEnable()
    {
        if (grid != null)
        {
            grid.OnGridReady += OnGridReady;
            if (grid.IsReady) OnGridReady();
        }
    }

    private void OnDisable()
    {
        if (grid != null)
            grid.OnGridReady -= OnGridReady;
    }

    private void OnGridReady()
    {
        RecalculateBounds();

        if (fitToGridOnStart)
            FitToBounds();
        else
            ClampNow();
    }

    private void Update()
    {
        if (cam == null) return;
        if (!_hasBounds) return;

        // Zoom
        if (!ignoreWhenPointerOverUI || !IsPointerOverUI())
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                float delta = -wheel * zoomSpeed;
                _targetOrtho = Mathf.Clamp(_targetOrtho + delta, minOrthoSize, GetMaxOrtho());
            }
        }

        // Pan drag
        if (!ignoreWhenPointerOverUI || !IsPointerOverUI())
        {
            if (Input.GetMouseButtonDown(panMouseButton))
            {
                _dragging = true;
                _dragStartWorld = ScreenToWorldOnGridPlane(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(panMouseButton))
                _dragging = false;

            if (_dragging && Input.GetMouseButton(panMouseButton))
            {
                Vector3 currentWorld = ScreenToWorldOnGridPlane(Input.mousePosition);
                Vector3 delta = _dragStartWorld - currentWorld;
                delta.y = 0f;

                _targetPos += delta * panSpeed;
            }
        }
        else
        {
            if (Input.GetMouseButtonUp(panMouseButton))
                _dragging = false;
        }

        _targetPos = ClampToBoundsViewportAware(_targetPos);

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, _targetOrtho, 1f - Mathf.Exp(-zoomSmooth * Time.deltaTime));
        transform.position = Vector3.Lerp(transform.position, _targetPos, 1f - Mathf.Exp(-panSmooth * Time.deltaTime));
    }

    public void RecalculateBounds()
    {
        if (grid == null) return;

        var cellsEnumerable = grid.GetAllCells();
        if (cellsEnumerable == null) return;

        bool any = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var c in cellsEnumerable)
        {
            if (c == null) continue;

            var r = c.GetComponentInChildren<Renderer>();
            if (r == null) continue;

            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        if (!any) return;

        b.Expand(new Vector3(boundsPadding * 2f, 0f, boundsPadding * 2f));

        _gridBounds = b;
        _hasBounds = true;

        _gridPlaneY = (Mathf.Abs(gridPlaneYOverride) > 0.0001f) ? gridPlaneYOverride : _gridBounds.center.y;
    }

    private void FitToBounds()
    {
        if (!_hasBounds || cam == null) return;

        Vector3 p = transform.position;
        p.x = _gridBounds.center.x;
        p.z = _gridBounds.center.z;
        _targetPos = p;

        float aspect = cam.aspect;

        float halfZ = _gridBounds.extents.z;
        float halfX = _gridBounds.extents.x;

        float sizeByHeight = halfZ;
        float sizeByWidth = halfX / aspect;

        float fit = Mathf.Max(sizeByHeight, sizeByWidth);
        fit = Mathf.Max(fit, minOrthoSize);

        _targetOrtho = fit;
        cam.orthographicSize = _targetOrtho;

        if (maxOrthoSize <= 0f)
            maxOrthoSize = fit * 1.35f;

        ClampNow();
    }

    private void ClampNow()
    {
        _targetPos = ClampToBoundsViewportAware(_targetPos);
        transform.position = _targetPos;
        if (cam != null) cam.orthographicSize = _targetOrtho;
    }

    private float GetMaxOrtho()
    {
        return (maxOrthoSize > 0f) ? maxOrthoSize : Mathf.Max(minOrthoSize, cam.orthographicSize);
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    private Vector3 ScreenToWorldOnGridPlane(Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, _gridPlaneY, 0f));

        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        return transform.position;
    }

    private Vector3 ClampToBoundsViewportAware(Vector3 desiredPos)
    {
        if (!_hasBounds || cam == null) return desiredPos;

        Vector3 currentPos = transform.position;
        Vector3 deltaPos = desiredPos - currentPos;

        Vector3 bl = ViewportToWorldOnPlane(new Vector2(0f, 0f), currentPos) + deltaPos;
        Vector3 tl = ViewportToWorldOnPlane(new Vector2(0f, 1f), currentPos) + deltaPos;
        Vector3 br = ViewportToWorldOnPlane(new Vector2(1f, 0f), currentPos) + deltaPos;
        Vector3 tr = ViewportToWorldOnPlane(new Vector2(1f, 1f), currentPos) + deltaPos;

        float minX = Mathf.Min(bl.x, tl.x, br.x, tr.x);
        float maxX = Mathf.Max(bl.x, tl.x, br.x, tr.x);
        float minZ = Mathf.Min(bl.z, tl.z, br.z, tr.z);
        float maxZ = Mathf.Max(bl.z, tl.z, br.z, tr.z);

        float shiftX = 0f;
        float shiftZ = 0f;

        if (minX < _gridBounds.min.x) shiftX += (_gridBounds.min.x - minX);
        if (maxX > _gridBounds.max.x) shiftX -= (maxX - _gridBounds.max.x);

        if (minZ < _gridBounds.min.z) shiftZ += (_gridBounds.min.z - minZ);
        if (maxZ > _gridBounds.max.z) shiftZ -= (maxZ - _gridBounds.max.z);

        float boundsSizeX = _gridBounds.size.x;
        float boundsSizeZ = _gridBounds.size.z;

        float visibleSizeX = maxX - minX;
        float visibleSizeZ = maxZ - minZ;

        Vector3 result = desiredPos;
        if (boundsSizeX <= visibleSizeX + 0.001f)
            result.x = _gridBounds.center.x;
        else
            result.x += shiftX;

        if (boundsSizeZ <= visibleSizeZ + 0.001f)
            result.z = _gridBounds.center.z;
        else
            result.z += shiftZ;

        return result;
    }

    private Vector3 ViewportToWorldOnPlane(Vector2 viewport, Vector3 camPos)
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        Vector3 originShift = camPos - transform.position;
        ray.origin += originShift;

        Plane plane = new Plane(Vector3.up, new Vector3(0f, _gridPlaneY, 0f));
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        return camPos;
    }
}
