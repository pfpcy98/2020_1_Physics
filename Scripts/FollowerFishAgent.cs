using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowerFishAgent : MonoBehaviour
{
    [SerializeField]
    public float _maxSpeed { get; private set; } = 0.03f;

    //// Evade 관련 변수
    [SerializeField]
    private float _fleeDistance = 5.0f;
    [SerializeField]
    private Transform _evadeTarget = null;

    //// Arrive 관련 변수
    [SerializeField]
    private float _deceleration = 2.0f;

    //// OffsetPursuit 관련 변수
    [SerializeField]
    private Transform _pursuitTarget = null;
    [SerializeField]
    private Vector3 _offset = Vector3.zero;

    //// WallAvoidance 관련 변수
    // 벽을 피하기 위한 더듬이의 길이
    [SerializeField]
    private float _feelerLength = 5.0f;

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
        // 기본 조종힘을 추가.
        _velocity = _velocity + ((OffsetPursuit() + WallAvoidance() + ObstacleAvoidance()) * Time.deltaTime);

        // 도망쳐야 하는 대상이 존재하면 대상에 대해 도피하는 조종힘을 추가
        // Evade는 대상이 _fleeDistance 거리 내에 있을 때만 조종힘을 반환한다.
        if (_evadeTarget != null)
        {
            _velocity = _velocity + Evade(_evadeTarget);
        }

        // 조종힘의 방향으로 보는 방향을 전환
        if (_velocity.magnitude > 0.005f)
        {
            transform.forward = _velocity.normalized;
        }

        transform.position = transform.position + _velocity;
    }

    private Vector3 Flee(Vector3 target_pos)
    {
        /// 거리 측정
        Vector3 distance = target_pos - transform.position;

        /// 거리가 회피 범위 이내일경우 회피 속도를 계산
        if (distance.sqrMagnitude < (_fleeDistance * _fleeDistance))
        {
            // Agent의 목적지에 대한 방향을 계산
            Vector3 vec3_AgentForwards = Vector3.Normalize(transform.position - target_pos) * _maxSpeed;

            // Agent의 목표 속도를 반환
            return (vec3_AgentForwards - _velocity);
        }

        // 이외의 경우엔 속도 변화를 반환하지 않음
        return Vector3.zero;
    }

    private Vector3 Evade(Transform target_Agent)
    {
        BirdAgent target_info = target_Agent.GetComponent<BirdAgent>();
        Vector3 target_speed = Vector3.zero;
        if (target_info != null)
        {
            target_speed = target_info._velocity;
        }

        Vector3 correctedTargetPos = new Vector3(target_Agent.position.x, transform.position.y, target_Agent.position.z);
        Vector3 dir = correctedTargetPos - transform.position;

        float lookAheadTime = 1.0f;
        if (target_info != null)
        {
            lookAheadTime = dir.magnitude / (_maxSpeed + target_info._maxSpeed);
        }

        return Flee(correctedTargetPos + (target_speed * lookAheadTime));
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

    private Vector3 OffsetPursuit()
    {
        if(_pursuitTarget != null)
        {
            // 오프셋을 적용한 좌표 계산
            Vector3 targetPos = _pursuitTarget.TransformPoint(_offset);
            // 오프셋 - Agent 간 거리 계산
            Vector3 distance = targetPos - transform.position;

            float lookAheadTime = distance.magnitude / (_pursuitTarget.GetComponent<LeaderFishAgent>()._maxSpeed + _maxSpeed);

            return Arrive(targetPos + (_pursuitTarget.GetComponent<LeaderFishAgent>()._velocity * lookAheadTime));
        }

        // 추적할 대상이 없을 경우 조종힘을 반환하지 않음.
        return Vector3.zero;
    }

    private Vector3 WallAvoidance()
    {
        // 더듬이로 장애물 측정
        Ray[] rays = new Ray[3];
        rays[0] = new Ray(transform.position, transform.forward);
        rays[1] = new Ray(transform.position, (transform.forward + -transform.right).normalized);
        rays[2] = new Ray(transform.position, (transform.forward + transform.right).normalized);

        RaycastHit[][] hitInfos = new RaycastHit[3][];
        for (int i = 0; i < hitInfos.Length; i++)
        {
            hitInfos[i] = Physics.RaycastAll(rays[i], _feelerLength);
        }

        Transform closestIP = null;
        float distToClosestIP = float.MaxValue;
        Vector3 intersectionPoint = Vector3.zero;
        int detectedFeelerIndex = -1;

        for (int i = 0; i < hitInfos.Length; i++)
        {
            if (hitInfos[i] != null)
            {
                for (int j = 0; j < hitInfos[i].Length; j++)
                {
                    // 더듬이에 닿은 장애물이 벽일 경우에만 로직 수행
                    if (hitInfos[i][j].transform.tag == "Wall")
                    {
                        // 해당 충돌체와의 거리를 비교
                        if (hitInfos[i][j].distance < distToClosestIP)
                        {
                            // 더 짧은 거리의 충돌체일 경우 해당 충돌체를 기록
                            distToClosestIP = hitInfos[i][j].distance;

                            closestIP = hitInfos[i][j].transform;

                            intersectionPoint = hitInfos[i][j].point;

                            detectedFeelerIndex = i;
                        }
                    }
                }
            }
        }

        // 더듬이로 피해야 하는 충돌체를 감지한 경우
        if (closestIP != null)
        {
            Vector3 target;

            // 과충돌한 거리를 계산 (더듬이의 끝 위치 - 충돌 지점)
            Vector3 overShoot = (transform.position + rays[detectedFeelerIndex].direction * _feelerLength) - intersectionPoint;

            // 해당 벽의 로컬좌표계로부터 Agent의 상대 위치를 측정,
            // z좌표가 음수일 경우 법선 벡터의 반대방향 벡터로 계산(벽의 뒷면)
            // 조종힘 계산 (벽의 법선벡터 * 과충돌 벡터의 크기)
            if (closestIP.InverseTransformPoint(transform.position).z < 0.0f)
            {
                target = -closestIP.forward * overShoot.magnitude;
            }
            else
            {
                target = closestIP.forward * overShoot.magnitude;
            }

            return target;
        }

        // 아닐 경우 값이 0인 벡터를 반환
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
