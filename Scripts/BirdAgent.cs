using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BirdAgent : MonoBehaviour
{
    [SerializeField]
    public float _maxSpeed { get; private set; } = 0.1f;

    ////Arrive 관련 변수
    [SerializeField]
    private float _deceleration = 2.0f;
    private Vector3 _pickPos = Vector3.zero;

    public Vector3 _velocity { get; private set; } = Vector3.zero;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            Vector3 mouse_pos = Input.mousePosition;

            RaycastHit hit;
            if (Physics.Raycast(Camera.main.ScreenToWorldPoint(mouse_pos), -Vector3.up, out hit, 1000))
            {
                _pickPos = hit.point;
            }
        }
        _pickPos.y = transform.position.y;

        // 조종힘을 계산
        _velocity = _velocity + Arrive(_pickPos);

        // 조종힘의 방향으로 보는 방향을 전환
        if (_velocity.magnitude > 0.005f)
        {
            transform.forward = _velocity.normalized;
        }

        transform.position = transform.position + _velocity;
    }

    private Vector3 Arrive(Vector3 target_pos)
    {
        Vector3 distance = target_pos - transform.position;

        if (distance.magnitude > 0.0f)
        {
            Vector3 to_target = target_pos - transform.position;

            float _speed = distance.magnitude / _deceleration;

            // 최대 속도로 제한.
            _speed = Mathf.Min(_speed, _maxSpeed);

            Vector3 desired_velocity = to_target / distance.magnitude * _speed;

            return (desired_velocity - _velocity);
        }

        return Vector3.zero;
    }
}
