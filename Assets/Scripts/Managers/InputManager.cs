using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [SerializeField] private InputActionAsset playerInputs;

    // Swipe Inputs
    private InputActionMap touchActionMap;
    private InputAction primaryContact;
    private InputAction primaryPosition;

    private InputAction touchPress;
    private InputAction touchPosition;

    [Header("Press Settings")]
    [SerializeField] private float maximumPressTime = 1f;
    private float pressStartTime;
    private Vector2 pressStartPosition;
    private float pressEndTime;
    private Vector2 pressEndPosition;
    private Vector2 pressWorldPosition;

    [Header("Swipe Settings")]
    [SerializeField] private float minimumDistance = .2f;
    [SerializeField] private float maximumTime = 1f;
    [SerializeField, Range(0, 1f)] private float directionThreshold;

    private Vector2 startPosition;
    private float startTime;
    private Vector2 endPosition;
    private float endTime;

    public event Action<Vector2, Vector2> OnSwipeRequested;
    public event Action<Vector2Int> OnScreenTapped;

    private void Awake() {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        touchActionMap = playerInputs.FindActionMap("Touch");
        primaryContact = touchActionMap.FindAction("PrimaryContact");
        primaryPosition = touchActionMap.FindAction("PrimaryPosition");
        touchPress = touchActionMap.FindAction("TouchPress");
        touchPosition = touchActionMap.FindAction("TouchPosition");
    }

    private void Start()
    {
        primaryContact.started += ctx => StartTouchPrimary(ctx);
        primaryContact.canceled += ctx => EndTouchPrimary(ctx);

        touchPress.performed += ctx => StartTouchPress(ctx);
        touchPress.canceled += ctx => EndTouchPress(ctx);
    }

    private void StartTouchPrimary(InputAction.CallbackContext ctx)
    {
        startPosition = Utils.ScreenToWorld(primaryPosition.ReadValue<Vector2>());
        startTime = Time.time;
    }

    private void EndTouchPrimary(InputAction.CallbackContext ctx)
    {
        endPosition = Utils.ScreenToWorld(primaryPosition.ReadValue<Vector2>());
        endTime = Time.time;

        DetectSwipe();
    }

    public Vector2 PrimaryPosition()
    {
        return Utils.ScreenToWorld(primaryPosition.ReadValue<Vector2>());
    }

    private void DetectSwipe()
    {
        if (Vector3.Distance(startPosition, endPosition) >= minimumDistance && (endTime - startTime) <= maximumTime)
        {
            Vector3 direction = endPosition - startPosition;
            Vector2 direction2D = new Vector2(direction.x, direction.y).normalized;
            Swipe(direction2D);
        }
    }

    private void Swipe(Vector2 direction)
    {
        // Swipe Up
        if (Vector2.Dot(Vector2.up, direction) > directionThreshold)
        {
            OnSwipeRequested?.Invoke(startPosition, Vector2.up);
        }
        // Swipe down
        else if (Vector2.Dot(Vector2.down, direction) > directionThreshold)
        {
            OnSwipeRequested?.Invoke(startPosition, Vector2.down);
        }
        // Swipe left
        else if (Vector2.Dot(Vector2.left, direction) > directionThreshold)
        {
            OnSwipeRequested?.Invoke(startPosition, Vector2.left);
        }
        // Swipe right
        else if (Vector2.Dot(Vector2.right, direction) > directionThreshold)
        {
            OnSwipeRequested?.Invoke(startPosition, Vector2.right);
        }
    }

    private void StartTouchPress(InputAction.CallbackContext ctx)
    {
        Vector2 screenPosition = touchPosition.ReadValue<Vector2>();
        pressWorldPosition  = Utils.ScreenToWorld(screenPosition);
        pressStartTime = Time.time;
        pressStartPosition = screenPosition;
    }

    private void EndTouchPress(InputAction.CallbackContext ctx)
    {
        pressEndTime = Time.time;
        pressEndPosition = touchPosition.ReadValue<Vector2>();

        if ((pressEndTime - pressStartTime) <= maximumPressTime && Vector2.Distance(pressStartPosition, pressEndPosition) <= 0.2f)
        {
            OnScreenTapped?.Invoke(Utils.CalculateGridLocation(pressWorldPosition));
        }
    }
}
