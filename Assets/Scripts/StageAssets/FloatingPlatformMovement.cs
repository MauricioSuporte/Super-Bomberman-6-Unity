using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FloatingPlatformMovement : MonoBehaviour
{
    [Header("Fixed X (world)")]
    [SerializeField] private float fixedX = -7.5f;

    [Header("Y Range (world)")]
    [SerializeField] private float startY = 0.5f;
    [SerializeField] private float endY = 3.5f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float speed = 2f;

    [Header("Pause at ends")]
    [SerializeField, Min(0f)] private float waitSeconds = 2f;

    [Header("Startup")]
    [SerializeField] private bool setInitialPositionOnAwake = true;
    [SerializeField] private bool startGoingToEnd = true;

    private bool _goingToEnd;
    private bool _waiting;
    private Coroutine _waitRoutine;

    void Awake()
    {
        _goingToEnd = startGoingToEnd;

        if (setInitialPositionOnAwake)
        {
            Vector3 p = transform.position;
            p.x = fixedX;
            p.y = startY;
            transform.position = p;
        }
        else
        {
            Vector3 p = transform.position;
            p.x = fixedX;
            transform.position = p;
        }
    }

    void OnDisable()
    {
        if (_waitRoutine != null)
        {
            StopCoroutine(_waitRoutine);
            _waitRoutine = null;
        }

        _waiting = false;
    }

    void Update()
    {
        Vector3 pos = transform.position;
        if (pos.x != fixedX)
        {
            pos.x = fixedX;
            transform.position = pos;
            pos = transform.position;
        }

        if (_waiting || speed <= 0f)
            return;

        float targetY = _goingToEnd ? endY : startY;

        float newY = Mathf.MoveTowards(pos.y, targetY, speed * Time.deltaTime);
        transform.position = new Vector3(fixedX, newY, pos.z);

        if (Mathf.Approximately(newY, targetY))
        {
            _waitRoutine = StartCoroutine(WaitAndFlip());
        }
    }

    private IEnumerator WaitAndFlip()
    {
        _waiting = true;
        yield return new WaitForSeconds(waitSeconds);
        _goingToEnd = !_goingToEnd;
        _waiting = false;
        _waitRoutine = null;
    }
}
