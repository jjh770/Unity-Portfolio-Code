/*
 * 역할: 스킬의 근접 범위 판정과 레벨별 투사체 방향 생성을 담당합니다.
 * 핵심 설계: 한 번의 스킬 안에서 같은 Collider에 중복 피해가 적용되지 않도록 타격 집합을 초기화해 사용합니다.
 */
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 컨트롤러에서 호출되는 실제 피해 판정과 투사체 팩토리 역할을 합니다.
/// </summary>
public class PlayerSkillRange : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlayerSkillData _skillData;

    [Header("Skill Point")]
    [SerializeField] private Transform _skillPoint;
    [SerializeField] private LayerMask _monsterLayers;

    [Header("Projectile")]
    [SerializeField] private Transform _projectileSpawnPoint;
    [SerializeField] private PlayerProjectilePool _projectilePool;

    private HashSet<Collider> _hitEnemiesThisSkill = new HashSet<Collider>();

    /// <summary>
    /// 새 스킬 판정 집합을 시작하고 근접 범위 안의 적에게 피해를 적용합니다.
    /// </summary>
    public void ExecuteSkillHit()
    {
        _hitEnemiesThisSkill.Clear();
        CheckHitDetection();
    }

    /// <summary>
    /// 단일 또는 다중 각도 배열을 순회해 투사체를 생성합니다.
    /// </summary>
    public void FireProjectile(bool isTriple = false)
    {
        if (_projectilePool == null) return;

        Transform spawnPoint = _projectileSpawnPoint != null ? _projectileSpawnPoint : transform;
        Vector3 spawnPosition = spawnPoint.position;
        Vector3 baseDirection = transform.forward;

        float[] angles = isTriple ? _skillData.TripleProjectileAngles : _skillData.SingleProjectileAngles;

        foreach (float angle in angles)
        {
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * baseDirection;
            SkillProjectile projectile = _projectilePool.GetSkillProjectile(spawnPosition, Quaternion.identity);
            if (projectile != null)
            {
                SetupProjectile(projectile, direction);
            }
        }
    }

    /// <summary>
    /// 스킬 데이터와 소유자 정보를 ProjectileConfig로 묶어 초기화합니다.
    /// </summary>
    private void SetupProjectile(SkillProjectile projectile, Vector3 direction)
    {
        var config = new ProjectileConfig
        {
            Direction = direction,
            Speed = _skillData.ProjectileSpeed,
            MaxDistance = _skillData.ProjectileRange,
            Width = _skillData.ProjectileWidth,
            Height = _skillData.ProjectileHeight,
            Depth = _skillData.ProjectileDepth,
            Damage = _skillData.SkillDamage,
            Owner = gameObject,
            MonsterLayers = _monsterLayers,
            OnReturn = _projectilePool.ReturnSkillProjectile
        };
        projectile.Initialize(config);
    }

    /// <summary>
    /// OverlapSphere 결과 중 아직 맞지 않은 대상에만 공통 DamageUtility를 호출합니다.
    /// </summary>
    private void CheckHitDetection()
    {
        if (_skillPoint == null)
        {
            _skillPoint = transform;
        }

        Collider[] hitColliders = Physics.OverlapSphere(_skillPoint.position, _skillData.SkillRange, _monsterLayers);

        foreach (Collider col in hitColliders)
        {
            if (_hitEnemiesThisSkill.Contains(col))
                continue;

            DamageUtility.ApplyDamage(col.gameObject, _skillData.SkillDamage, gameObject);
            _hitEnemiesThisSkill.Add(col);
        }
    }

    public float SkillRange => _skillData.SkillRange;
    public float SkillDamage => _skillData.SkillDamage;

    private void OnDrawGizmosSelected()
    {
        if (_skillData == null) return;

        // 착지 범위 표시
        Transform point = _skillPoint != null ? _skillPoint : transform;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(point.position, _skillData.SkillRange);

        // 투사체 범위 표시
        Transform spawnPoint = _projectileSpawnPoint != null ? _projectileSpawnPoint : transform;
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Vector3 endPoint = spawnPoint.position + transform.forward * _skillData.ProjectileRange;
        Gizmos.DrawLine(spawnPoint.position, endPoint);
        Gizmos.DrawWireCube(endPoint, new Vector3(_skillData.ProjectileWidth, _skillData.ProjectileHeight, _skillData.ProjectileDepth));
    }
}
