using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    public Vector2? firstClickPosition;
    public Vector2? secondClickPosition;

    public event Action<Vector2, Vector2> OnSwapRequested;

    private void Awake() {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        firstClickPosition = null;
        secondClickPosition = null;
    }

    private void Update()
    {
        // Later change this to swiping.
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 position = Camera.main.ScreenToWorldPoint(Mouse.current.position.value);

            if (firstClickPosition == null)
            {
                firstClickPosition = new Vector2(position.x, position.y);
            }
            else
            {
                secondClickPosition = new Vector2(position.x, position.y);
                OnSwapRequested?.Invoke(firstClickPosition.Value, secondClickPosition.Value);

                firstClickPosition = null;
                secondClickPosition = null;
            }
        }
    }
}
