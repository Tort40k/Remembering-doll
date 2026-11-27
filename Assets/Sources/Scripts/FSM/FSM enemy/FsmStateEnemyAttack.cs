using UnityEngine;

public class FsmStMeleeEnemyAttack : FsmState
{
    private readonly EnemyMelee _enemy;

    public FsmStMeleeEnemyAttack(Fsm fsm, EnemyMelee enemy) : base(fsm)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log($"{this} state is [ENTER]");
    }

    public override void Exit()
    {
        Debug.Log($"{this} state is [EXIT]");
    }

    public override void Update()
    {
        if (_enemy.Target == null || _enemy.Controller == null) return;

        _enemy.ApplyVerticalGravityOnly();

        if (!_enemy.IsTargetInAttackRange())
        {
            Fsm.SetState<FsmStEnemyMeleeChase>();
        }
    }
}