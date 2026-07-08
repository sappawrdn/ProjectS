using UnityEngine;
using UnityEngine.AI;

namespace ProjectS
{
    /// <summary>
    /// Drives the ghost model's Animator from the monster's movement — purely cosmetic; the AI stays on
    /// MonsterAI. Lives on the model (a child of the Monster object that owns the NavMeshAgent). Walk when the
    /// agent is moving, Idle when still; PlayAttack() fires the lunge (scares). Root motion is off so the
    /// NavMeshAgent keeps driving position.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class MonsterVisual : MonoBehaviour
    {
        [SerializeField] private float _moveThreshold = 0.1f; // agent m/s that counts as walking

        private Animator _anim;
        private NavMeshAgent _agent;

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            _agent = GetComponentInParent<NavMeshAgent>();
        }

        private void Update()
        {
            if (_anim == null) return;
            float speed = _agent != null ? _agent.velocity.magnitude : 0f;
            _anim.SetBool("Moving", speed > _moveThreshold);
        }

        /// <summary>Fire the attack/lunge animation (scripted scares).</summary>
        public void PlayAttack()
        {
            if (_anim != null) _anim.SetTrigger("Attack");
        }
    }
}
