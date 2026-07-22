/*
 * 역할: 설정 객체를 받아 이동, 범위 충돌, 중복 타격 방지와 풀 반환을 수행하는 스킬 투사체입니다.
 * 핵심 설계: 공격별 런타임 수치를 ProjectileConfig로 전달해 프리팹과 발사 로직의 결합을 줄입니다.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투사체 한 발의 이동·판정·피해와 반환 정책을 전달하는 초기화 데이터입니다.
/// </summary>
public struct ProjectileConfig
{
    public Vector3 Direction;
    public float Speed;
    public float MaxDistance;
    public float Width;
    public float Height;
    public float Depth;
    public float Damage;
    public GameObject Owner;
    public LayerMask MonsterLayers;
    public Action<GameObject> OnReturn;
}

/// <summary>
/// 풀 재사용 가능한 투사체의 이동과 생명주기를 관리합니다.
/// </summary>
public class SkillProjectile : MonoBehaviour, IPoolable
{
    [Header("Settings")]
    [SerializeField] private LayerMask _monsterLayers;

    private float _speed;
    private float _maxDistance;
    private float _width;
    private float _height;
    private float _depth;
    private float _damage;
    private Vector3 _direction;
    private Vector3 _startPosition;
    private GameObject _owner;
    // 최대 거리 도달 후 구체적인 풀 구현으로 반환하기 위한 콜백입니다.
    private Action<GameObject> _onReturn;

    private HashSet<Collider> _hitEnemies = new HashSet<Collider>();
    private bool _isInitialized = false;

    /// <summary>
    /// 풀에서 재사용되기 전 초기화 플래그와 타격 기록을 정리합니다.
    /// </summary>
    public void OnSpawn()
    {
        _hitEnemies.Clear();
        _isInitialized = false;
    }

    /// <summary>
    /// 비활성화 시 외부 콜백과 런타임 상태를 제거합니다.
    /// </summary>
    public void OnDespawn()
    {
        _isInitialized = false;
    }

    /// <summary>
    /// 모든 런타임 설정을 복사하고 시작 위치와 회전, 중복 타격 상태를 초기화합니다.
    /// </summary>
    public void Initialize(ProjectileConfig config)
    {
        _direction = config.Direction.normalized;
        _speed = config.Speed;
        _maxDistance = config.MaxDistance;
        _width = config.Width;
        _height = config.Height;
        _depth = config.Depth;
        _damage = config.Damage;
        _owner = config.Owner;
        _monsterLayers = config.MonsterLayers;
        _onReturn = config.OnReturn;
        _startPosition = transform.position;
        _isInitialized = true;

        transform.rotation = Quaternion.LookRotation(_direction);
    }

    private void Update()
    {
        if (!_isInitialized) return;

        Move();
        CheckHit();
        CheckMaxDistance();
    }

    private void Move()
    {
        transform.position += _direction * _speed * Time.deltaTime;
    }

    /// <summary>
    /// 현재 회전이 적용된 OverlapBox로 범위를 검사하고 새로운 대상에만 피해를 줍니다.
    /// </summary>
    private void CheckHit()
    {
        Vector3 halfExtents = new Vector3(_width / 2f, _height / 2f, _depth / 2f);
        Collider[] hitColliders = Physics.OverlapBox(transform.position, halfExtents, transform.rotation, _monsterLayers);

        foreach (Collider col in hitColliders)
        {
            if (_hitEnemies.Contains(col))
                continue;

            DamageUtility.ApplyDamage(col.gameObject, _damage, _owner);
            _hitEnemies.Add(col);
        }
    }

    /// <summary>
    /// 실제 이동한 제곱 거리를 기준으로 수명이 끝난 투사체를 풀에 반환합니다.
    /// </summary>
    private void CheckMaxDistance()
    {
        float distanceTraveledSqr = (transform.position - _startPosition).sqrMagnitude;
        if (distanceTraveledSqr >= _maxDistance * _maxDistance)
        {
            _onReturn?.Invoke(gameObject);
        }
    }

    private void OnDrawGizmos()
    {
        if (!_isInitialized) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(_width, _height, _depth));
    }
}
