using UnityEngine;

public sealed class PlayerClickMoveV2 : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private bool holdToMove = true;
    [SerializeField] private float holdUpdateMinDelta = 0.35f;
    [SerializeField] private float holdUpdateRate = 20f;

    [Header("Movement")]
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float deceleration = 22f;
    [SerializeField] private float slowRadius = 1.5f;
    [SerializeField] private float stopDistance = 0.15f;

    [Header("Rotation")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private bool rotateOnlyWhenMoving = true;

    [Header("Plane Lock")]
    [SerializeField] private bool lockY = true;
    [SerializeField] private float lockedY = 0f;

    [Header("Physics (optional)")]
    [SerializeField] private Rigidbody rb;

    [Header("World Bounds")]
    [Tooltip("Player will be clamped inside this BoxCollider bounds. Prefer unrotated collider.")]
    [SerializeField] private BoxCollider worldBounds;

    [Tooltip("How far from the bounds edge the player is kept (world units).")]
    [SerializeField] private float boundsPadding = 0.25f;

    private Camera _cam;
    private Vector3 _target;
    private bool _hasTarget;

    private float _currentSpeed;
    private float _nextHoldUpdateTime;

    private void Awake()
    {
        _cam = Camera.main;

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (visualRoot == null)
            visualRoot = transform;

        if (!lockY)
            lockedY = transform.position.y;
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (TryGetPointUnderMouse(out var p))
            {
                p = ClampToBounds(p);
                _target = p;
                _hasTarget = true;
                _nextHoldUpdateTime = Time.time;
            }
        }

        if (holdToMove && Input.GetMouseButton(0))
        {
            if (!(holdUpdateRate > 0f && Time.time < _nextHoldUpdateTime))
            {
                if (TryGetPointUnderMouse(out var p))
                {
                    p = ClampToBounds(p);

                    if (!_hasTarget || Vector3.Distance(_target, p) >= holdUpdateMinDelta)
                    {
                        _target = p;
                        _hasTarget = true;

                        if (holdUpdateRate > 0f)
                            _nextHoldUpdateTime = Time.time + 1f / holdUpdateRate;
                    }
                }
            }
        }

        if (rb == null)
            TickMove(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (rb != null)
            TickMove(Time.fixedDeltaTime);
    }

    private bool TryGetPointUnderMouse(out Vector3 point)
    {
        point = default;
        if (_cam == null) return false;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 5000f, groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 p = hit.point;
            if (lockY) p.y = lockedY;
            point = p;
            return true;
        }

        return false;
    }

    private void TickMove(float dt)
    {
        if (!_hasTarget || dt <= 0f) return;

        Vector3 pos = GetPosition();
        pos = ClampToBounds(pos);

        Vector3 to = _target - pos;
        if (lockY) to.y = 0f;

        float dist = to.magnitude;

        if (dist <= stopDistance)
        {
            _currentSpeed = MoveTowards(_currentSpeed, 0f, deceleration * dt);
            ApplyMove(Vector3.zero, dt);

            if (_currentSpeed <= 0.01f)
            {
                _currentSpeed = 0f;
                _hasTarget = false;
            }
            return;
        }

        Vector3 dir = to / Mathf.Max(0.0001f, dist);

        float desiredSpeed = maxSpeed;
        if (dist < slowRadius)
        {
            float t = Mathf.Clamp01(dist / Mathf.Max(0.0001f, slowRadius));
            desiredSpeed = maxSpeed * t;
        }

        float accel = (_currentSpeed < desiredSpeed) ? acceleration : deceleration;
        _currentSpeed = MoveTowards(_currentSpeed, desiredSpeed, accel * dt);

        ApplyMove(dir * _currentSpeed, dt);

        if (visualRoot != null && (!rotateOnlyWhenMoving || _currentSpeed > 0.05f))
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            visualRoot.rotation = Quaternion.RotateTowards(visualRoot.rotation, targetRot, turnSpeed * dt);
        }
    }

    private Vector3 GetPosition() => rb != null ? rb.position : transform.position;

    private void ApplyMove(Vector3 velocity, float dt)
    {
        Vector3 pos = GetPosition();
        Vector3 newPos = pos + velocity * dt;
        if (lockY) newPos.y = lockedY;

        newPos = ClampToBounds(newPos);

        if (rb != null) rb.MovePosition(newPos);
        else transform.position = newPos;
    }

    private Vector3 ClampToBounds(Vector3 p)
    {
        if (worldBounds == null) return p;

        Bounds b = worldBounds.bounds;

        float minX = b.min.x + boundsPadding;
        float maxX = b.max.x - boundsPadding;
        float minZ = b.min.z + boundsPadding;
        float maxZ = b.max.z - boundsPadding;

        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.z = Mathf.Clamp(p.z, minZ, maxZ);

        if (lockY) p.y = lockedY;
        return p;
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (maxDelta <= 0f) return current;
        if (current < target) return Mathf.Min(current + maxDelta, target);
        return Mathf.Max(current - maxDelta, target);
    }

    private void OnDrawGizmosSelected()
    {
        if (!_hasTarget) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_target, 0.2f);
    }
}
