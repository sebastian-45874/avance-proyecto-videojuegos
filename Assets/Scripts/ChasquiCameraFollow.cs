using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class ChasquiCameraFollow : MonoBehaviour
{
    public Transform target;
    [SerializeField, Min(.01f)] float smoothTime = .18f;
    Vector3 velocity;
    Camera view;
    void Awake() { view = GetComponent<Camera>(); }
    void Start() { if (target != null) transform.position = DesiredPosition(); }
    Vector3 DesiredPosition()
    {
        float h = view.orthographicSize, w = h * view.aspect;
        return new Vector3(w >= 18 ? 0 : Mathf.Clamp(target.position.x, -18 + w, 18 - w),
            h >= 12 ? 0 : Mathf.Clamp(target.position.y + .6f, -12 + h, 12 - h), -10);
    }
    void LateUpdate()
    {
        if (target != null) transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, smoothTime);
    }
}
