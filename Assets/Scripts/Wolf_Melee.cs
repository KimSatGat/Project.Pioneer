﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Wolf_Melee : LivingObject
{
    enum EnemyState { IDLE, TRACE, ATTACK, DIE, GAUGING };

    private Coroutine findNearPlayer;   // 추적 코루틴 변수
    private Coroutine myUpdate;         // Update 코루틴 변수

    private Player[] players;           // 추적할 플레이어 리스트
    private Player target;              // 가장 가까운 플레이어
    private EnemyState enemyState;      // 적 상태
    private Vector3 pivot;              // 피봇
    private CameraShake cameraShake;

    public GameObject attackUI;         // 공격 UI
    public Image attackGauge;           // 공격게이지 UI
    public Transform detectPoint;       // 공격 감지 피봇
    public Vector2 detectRange;         // 공격 감지 범위
    public float[] lens = { 1.53f, 1.48f, 1.39f, 1.28f, 1.155f };   // 공격 감지 레이캐스트 마다 길이 할당 -> 기즈모로 직접 길이 구함..
    List<Vector2> dirs = new List<Vector2>();   // 공격 감지 방향 리스트

    private Material material;
    private Color materialTintColor;    // 틴트 효과를 위한 색상값
    private string dissolveShader = "Shader Graphs/Dissolve";
    private float dissolveAmout = 0f;   // Dissolve 효과 값 


    private Animator animator;
    private Rigidbody2D rigidbody2D;
    private HealthBarFade healthBarFade;    // 체력바

    protected override void OnEnable()
    {
        base.OnEnable(); // InitObject()
    }

    // 능력치, 상태 값 설정
    public override void InitObject()
    {
        startingHP = 100f;
        HP = startingHP;
        damage = 10f;
        moveSpeed = 30f;
        attackSpeed = 5f;
        dead = false;
        enemyState = EnemyState.IDLE;   // 적 상태
        dir = 1;                        // 오른쪽 방향 할당
    }

    void Awake()
    {
        players = GameObject.FindObjectsOfType<Player>();  // 플레이어 리스트 담기
        cameraShake = GameObject.FindObjectOfType<CameraShake>();   // 메인카메라의 CameraShake 컴포넌트 할당

        animator = GetComponent<Animator>();
        rigidbody2D = GetComponent<Rigidbody2D>();
        material = GetComponent<SpriteRenderer>().material;
        healthBarFade = GetComponentInChildren<HealthBarFade>();

        materialTintColor = new Color(1f, 0f, 0f, 150f / 255f);

        // 공격 감지 레이캐스트 방향 할당
        for (int i = 0; i < 5; i++)
        {
            dirs.Add(new Vector2(
                Mathf.Cos((5 + 13.75f * i) * Mathf.Deg2Rad),
                Mathf.Sin((5 + 13.75f * i) * Mathf.Deg2Rad)
                ));
        }
    }

    void Start()
    {
        findNearPlayer = StartCoroutine(FindNearPlayer(10f)); // 추적 코루틴 변수에 할당, 10초 마다 실행
        myUpdate = StartCoroutine(MyUpdate());

        onDeath += OffAttackUI;     // 죽었을 때 이벤트 추가
        onDeath += SetOnDeath;
    }


    IEnumerator MyUpdate()
    {
        while (!dead)
        {
            switch (enemyState)
            {
                case EnemyState.IDLE:
                case EnemyState.TRACE:
                    DetectPlayer();  // 감지
                    TracePlayer();   // 감지한 플레이어한테 이동
                    break;
                case EnemyState.GAUGING:
                    Gauging();
                    break;
                case EnemyState.ATTACK:
                    Attack();
                    break;
                case EnemyState.DIE:
                    break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    void Attack()
    {
        // 공격 모션 -> 게이지 채우기 -> 다 채웠으면 공격

        // 공격 모션               
        animator.SetInteger("State", (int)enemyState);
        rigidbody2D.velocity = new Vector2(0f, 0f);
    }


    // 공격 감지 기즈모
    public void OnDrawGizmos()
    {
        // Draw a yellow sphere at the transform's position
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawCube(detectPoint.position, detectRange);
    }

    // n초 마다 가장 가까운 플레이어를 찾기
    IEnumerator FindNearPlayer(float n)
    {
        while (true)
        {
            // 둘다 죽었다면 찾기 중지
            if (players[0].dead && players[1].dead)
            {
                target = null;

                // 코루틴 정지 후 while문 종료
                StopCoroutine(findNearPlayer);
                break;
            }

            // P1가 살고 P2가 죽었다면
            else if (!players[0].dead && players[1].dead)
            {
                target = players[0];
            }

            // P1가 죽고 P2가 살았다면
            else if (players[0].dead && !players[1].dead)
            {
                target = players[1];
            }

            // 둘 다 살았다면
            else
            {
                // 거리 측정        
                float EnemytoP1 = Vector2.Distance(players[0].transform.position, transform.position);
                float EnemytoP2 = Vector2.Distance(players[1].transform.position, transform.position);

                // P1이 더 가깝다면
                if (EnemytoP1 < EnemytoP2)
                {
                    // P1 할당
                    target = players[0];
                }

                // P2가 더 가깝다면
                else
                {
                    // P2 할당
                    target = players[1];
                }
            }

            // 10초 마다 실행
            yield return new WaitForSeconds(n);
        }
    }

    void DetectPlayer()
    {
        // 공격 감지 범위 구현
        Collider2D[] hits = Physics2D.OverlapBoxAll(detectPoint.position, detectRange, 0f);

        foreach (Collider2D hit in hits)
        {
            if (hit)
            {
                // 플레이어를 감지 했다면
                if (hit.tag == "Player")
                {
                    enemyState = EnemyState.ATTACK;
                    return;
                }
            }
            else
            {
                Debug.Log("플레이어 미발견");
            }
        }
    }

    void Gauging()
    {
        if (attackGauge.fillAmount >= 1f)
        {
            attackGauge.fillAmount = 0f;
            animator.speed = 1f;
            enemyState = EnemyState.ATTACK;
            return;
        }

        attackGauge.fillAmount += attackSpeed / 100f;
    }

    // Attack 애니메이션 이벤트 함수 -> 공격 게이지 시작
    public void StartAttackGauge()
    {
        animator.speed = 0f;
        enemyState = EnemyState.GAUGING;
    }

    // Attack 애니메이션 이벤트 함수 -> 공격 끝 -> 범위 내 플레이어에게 데미지 적용
    public void EndAttack()
    {
        // 피봇 구하기
        pivot = transform.position + new Vector3(0f, -0.5f, 0f);

        // 레이캐스트 기즈모 표시

        for (int i = 0; i < 5; i++)
        {
            Debug.DrawRay(pivot, new Vector3(dir, 1f, 0) * dirs[i] * lens[i], Color.yellow);
        }

        // 플레이어에게 데미지를 주었는지 판단
        bool isDamaed = false;
        // 레이캐스트 
        for (int i = 0; i < 5; i++)
        {
            // 데미지를 주었다면 break
            if (isDamaed)
            {
                break;
            }
            RaycastHit2D[] hits = Physics2D.RaycastAll(pivot, new Vector3(dir, 1f, 0) * dirs[i], lens[i]);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider.tag == "Player")
                {
                    // 플레이어 데미지 주기

                    // 데미지는 한번만 주기 때문에 true
                    isDamaed = true;

                    LivingObject livingObject = hit.collider.gameObject.GetComponent<LivingObject>();
                    livingObject.OnDamage(damage);
                    // foreach문 빠져나가기
                    break;
                }
            }
        }

        enemyState = EnemyState.IDLE;
        animator.SetInteger("State", (int)enemyState);
    }

    // 이동 메서드
    void TracePlayer()
    {
        if (target != null)
        {
            if (enemyState == EnemyState.ATTACK)
            {
                return;
            }

            // 상태 설정
            enemyState = EnemyState.TRACE;
            // 애니메이터 파라미터 할당
            animator.SetInteger("State", (int)enemyState);

            // 방향 구하기
            Vector2 moveDir = (target.transform.position - transform.position).normalized;

            // 바라보는 방향 설정
            if (moveDir.x > 0f)
            {
                transform.rotation = new Quaternion(0f, 0f, 0f, 0f);
                dir = 1;
            }
            else
            {
                transform.rotation = new Quaternion(0f, 180f, 0f, 0f);
                dir = -1;
            }

            // 이동            
            rigidbody2D.velocity = moveDir * moveSpeed * Time.deltaTime;
        }
    }

    public override void OnDamage(float damage)
    {

        // 체력 감소
        base.OnDamage(damage);

        // 틴트 효과
        StartCoroutine(SetTint());

        // HIT 효과
        DamagePopup.Create(transform.position, false);

        // HP UI 감소 효과
        healthBarFade.healthSystem.Damage((int)damage);

        // 카메라 흔들림
        StartCoroutine(cameraShake.ShakeCamera(0.01f, 0.05f));
    }

    // 틴트 효과
    IEnumerator SetTint()
    {
        while (true)
        {
            if (materialTintColor.a > 0)
            {
                materialTintColor.a = Mathf.Clamp01(materialTintColor.a - 6f * Time.deltaTime);

                material.SetColor("_Tint", materialTintColor);
            }
            else
            {
                materialTintColor = new Color(1f, 0f, 0f, 150f / 255f);
                StopCoroutine(SetTint());
                break;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    // Dissolve 효과
    IEnumerator SetDissolve()
    {
        // 셰이더 변경
        material.shader = Shader.Find(dissolveShader);
        float dissolveSpeed = 5f;
        while (true)
        {
            if (dissolveAmout < 1f)
            {
                dissolveAmout = Mathf.Clamp01(dissolveAmout + dissolveSpeed * Time.deltaTime);
                material.SetFloat("_DissolveAmount", dissolveAmout);
            }

            else
            {
                break;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    void SetOnDeath()
    {
        animator.speed = 0f;
        StartCoroutine(SetDissolve());
        Destroy(gameObject, 1f);
    }

    public void OnAttackUI()
    {
        attackUI.SetActive(true);
    }
    public void OffAttackUI()
    {
        attackUI.SetActive(false);
    }
}