using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public sealed class ChasquiMovement : MonoBehaviour
{
    [SerializeField, Min(0)] float speed = 3.5f;
    public float SpeedMultiplier { get; set; } = 1f;
    Rigidbody2D body;
    Animator animator;
    SpriteRenderer sprite;
    Vector2 movement, touchInput;
    bool touchActive;
    public bool ClickOnly { get; set; }
    string direction = "Down", state;
    void Awake() { body = GetComponent<Rigidbody2D>(); animator = GetComponent<Animator>(); sprite = GetComponent<SpriteRenderer>(); }
    // A future touch joystick can provide input without changing the movement logic.
    public void SetTouchInput(Vector2 value) { touchActive = true; touchInput = Vector2.ClampMagnitude(value, 1); }
    public void ReleaseTouchInput() { touchActive = false; touchInput = Vector2.zero; }
    void Update()
    {
        Vector2 input = touchActive ? touchInput : ClickOnly ? Vector2.zero : ReadKeyboard();
        movement = input.sqrMagnitude < .04f ? Vector2.zero :
            Mathf.Abs(input.x) > Mathf.Abs(input.y) ? new Vector2(Mathf.Sign(input.x), 0) : new Vector2(0, Mathf.Sign(input.y));
        if (movement != Vector2.zero)
            direction = movement.x > 0 ? "Right" : movement.x < 0 ? "Left" : movement.y > 0 ? "Up" : "Down";
        string next = direction + (movement == Vector2.zero ? "_Idle" : "_Walk");
        if (next != state) { animator.Play(next, 0, 0); state = next; }
    }
    static Vector2 ReadKeyboard()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        return new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
            (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
#elif ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
        return Vector2.zero;
#endif
    }
    void FixedUpdate() { body.linearVelocity = movement * speed * SpeedMultiplier; }
    void LateUpdate() { sprite.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 10); }
    void OnApplicationFocus(bool focused) { if (!focused) Stop(); }
    void OnDisable() { Stop(); }
    void Stop() { movement = touchInput = Vector2.zero; touchActive = false; if (body != null) body.linearVelocity = Vector2.zero; }
}

