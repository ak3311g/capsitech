using UnityEngine;

public class InputManager : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private GameManager gameManager;

    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 3f;
    [SerializeField] private float maxSwipeTime = 1f;

    private Vector2 _startPosition;
    private float _startTime;
    private bool _isSwiping = false;

    void Update()
    {
        // --- Mouse Input (Editor Testing) ---
        if (Input.GetMouseButtonDown(0))
        {
            _startPosition = Input.mousePosition;
            _startTime = Time.time;
            _isSwiping = true;
            Debug.Log($"Mouse DOWN at: {_startPosition}");
        }

        if (Input.GetMouseButtonUp(0) && _isSwiping)
        {
            Vector2 endPosition = Input.mousePosition;
            _isSwiping = false;
            Debug.Log($"Mouse UP at: {endPosition}");
            DetectSwipe(_startPosition, endPosition);
        }

        // --- Touch Input (Mobile) ---
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _startPosition = touch.position;
                    _startTime = Time.time;
                    _isSwiping = true;
                    Debug.Log($"Touch DOWN at: {_startPosition}");
                    break;

                case TouchPhase.Ended:
                    if (_isSwiping)
                    {
                        Vector2 endPos = touch.position;
                        _isSwiping = false;
                        Debug.Log($"Touch UP at: {endPos}");
                        DetectSwipe(_startPosition, endPos);
                    }
                    break;
                    
                case TouchPhase.Canceled:
                    _isSwiping = false;
                    Debug.Log("Touch canceled");
                    break;
            }
        }
    }

    private void DetectSwipe(Vector2 startPos, Vector2 endPos)
    {
        Vector2 swipeDelta = endPos - startPos;
        float swipeDistance = swipeDelta.magnitude;
        float swipeTime = Time.time - _startTime;

        Debug.Log($"Swipe Distance: {swipeDistance}, Time: {swipeTime}");

        if (swipeDistance < minSwipeDistance)
        {
            Debug.Log($"Swipe too short! Distance: {swipeDistance}");
            return;
        }

        if (swipeTime > maxSwipeTime)
        {
            Debug.Log($"Swipe too slow! Time: {swipeTime}");
            return;
        }

        float x = Mathf.Abs(swipeDelta.x);
        float y = Mathf.Abs(swipeDelta.y);

        Direction dir;

        if (x > y)
            dir = swipeDelta.x > 0 ? Direction.Right : Direction.Left;
        else
            dir = swipeDelta.y > 0 ? Direction.Up : Direction.Down;

        Debug.Log($"Swipe DIRECTION: {dir}");

        if (gameManager != null)
            gameManager.OnPlayerMove(dir);
        else
            Debug.LogError("GameManager is null!");
    }
}