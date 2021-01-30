using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButterflyAgent : MonoBehaviour
{
    [SerializeField]
    public float _maxSpeed { get; private set; } = 0.01f;

    ////Arrive 관련 변수
    [SerializeField]
    private float _deceleration = 2.0f;

    //// FollowPath 관련 변수
    private int _waypointIndex = 0;
    [SerializeField]
    private Transform[] _waypoints = null;
    // 도착이라고 인식하는 웨이포인트까지의 거리
    [SerializeField]
    private float _recognizationDistance = 0.1f;

    //// ObstacleAvoidance 관련 변수
    // 탐지 최소 거리
    [SerializeField]
    private float _detectionMinDistance = 5.0f;
    // 탐지 최대 거리
    [SerializeField]
    private float _detectionMaxDistance = 5.0f;
    // 좌우 움직임 가중치
    [SerializeField]
    private float _breakingWeight = 0.2f;

    public Vector3 _velocity { get; private set; } = Vector3.zero;

    // Update is called once per frame
    void Update()
    {
        _velocity = _velocity + ((FollowPath() + ObstacleAvoidance()) * Time.deltaTime);

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

    private Vector3 FollowPath()
    {
        if (_waypoints != null)
        {
            if (_waypoints[_waypointIndex] != null)
            {
                // 웨이포인트에 도착했다고 인식되면 다음 웨이포인트로 목표를 변경
                if ((_waypoints[_waypointIndex].position - transform.position).magnitude < _recognizationDistance)
                {
                    if ((_waypointIndex + 1) >= _waypoints.Length)
                    {
                        _waypointIndex = 0;
                    }
                    else
                    {
                        _waypointIndex++;
                    }
                }

                // 해당 목표로 이동
                return Arrive(_waypoints[_waypointIndex].position);
            }
        }

        // 웨이포인트가 없을 경우 조종힘을 반환하지 않음.
        return Vector3.zero;
    }

    private Vector3 ObstacleAvoidance()
    {
        // 속도에 비례한 탐지 반경 산출
        float cast_length = _detectionMinDistance + ((_velocity.magnitude / _maxSpeed) * _detectionMaxDistance);

        // 탐지 반경 내의 장애물을 감지
        RaycastHit[] detectionInfos = Physics.BoxCastAll(transform.position, (transform.lossyScale / 2), transform.forward, Quaternion.identity, cast_length);

        // 장애물이 체크된 경우에만 로직을 수행
        if (detectionInfos.Length > 0 && detectionInfos != null)
        {
            Transform tr_closestIO = null;
            float distToClosestIP = float.MaxValue;

            for (int i = 0; i < detectionInfos.Length; i++)
            {
                // 감지된 객체가 장애물일 경우에만 로직 수행
                if (detectionInfos[i].transform.tag == "Obstacle")
                {
                    // 충돌체의 월드 좌표를 Agent 기준 로컬 좌표로 변환
                    Vector3 localCoord_Detected = transform.InverseTransformPoint(detectionInfos[i].transform.position);

                    // Agent 전방 여부 체크
                    if (localCoord_Detected.z >= 0)
                    {
                        // 충돌체의 반지름 계산
                        float objectRadius = detectionInfos[i].transform.localScale.x * detectionInfos[i].transform.GetComponent<SphereCollider>().radius;

                        float expendedRadius = objectRadius + (transform.localScale.x / 2);

                        // 충돌체의 로컬 x 좌표 < (충돌체의 반지름 + 충돌박스 크기의 절반) 인지 체크
                        // 참일 경우, 충돌 가능성이 있는 충돌체임.
                        if (Mathf.Abs(localCoord_Detected.x) < expendedRadius)
                        {
                            // 충돌체와 충돌박스의 교점을 구하고 비교로 가장 가까운 충돌체를 구한다.
                            float sqrtPart = Mathf.Sqrt(expendedRadius * expendedRadius - localCoord_Detected.x * localCoord_Detected.x);

                            float ip = localCoord_Detected.z - sqrtPart;
                            if (ip <= 0.0f)
                            {
                                ip = localCoord_Detected.z + sqrtPart;
                            }

                            if (ip < distToClosestIP)
                            {
                                distToClosestIP = ip;
                                tr_closestIO = detectionInfos[i].transform;
                            }
                        }
                    }
                }
            }

            // 가까운 충돌체를 회피하는 조종힘을 계산
            if (tr_closestIO != null)
            {
                Vector3 steeringForce = Vector3.zero;

                float multiplier = 1.0f + ((cast_length - transform.InverseTransformPoint(tr_closestIO.position).z) / cast_length);

                // 충돌체의 반지름 계산
                float radius = tr_closestIO.localScale.x * tr_closestIO.transform.GetComponent<SphereCollider>().radius;

                // 로컬 좌표상에서의 조종힘 계산
                steeringForce.x = (radius - transform.InverseTransformPoint(tr_closestIO.position).x) * multiplier;
                steeringForce.z = (radius - transform.InverseTransformPoint(tr_closestIO.position).z) * _breakingWeight;

                // 조종힘을 월드좌표 기준으로 변환하여 반환
                return (transform.TransformPoint(steeringForce) - transform.position);
            }
        }

        return Vector3.zero;
    }
}
