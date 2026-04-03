using UnityEngine;

public sealed class CameraFollowNoRotateV2 : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Tooltip("Local offset from target in world space.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 20f, -12f);

    [Tooltip("If true, uses SmoothDamp for position.")]
    [SerializeField] private bool smooth = true;

    [SerializeField] private float smoothTime = 0.12f;

    [Tooltip("Keep camera rotation fixed (recommended).")]
    [SerializeField] private bool keepFixedRotation = true;

    [SerializeField] private Vector3 fixedEuler = new Vector3(60f, 0f, 0f);

    private Vector3 _vel;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + offset;

        if (smooth)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _vel, smoothTime);
        else
            transform.position = desiredPos;

        if (keepFixedRotation)
            transform.rotation = Quaternion.Euler(fixedEuler);
    }
}
