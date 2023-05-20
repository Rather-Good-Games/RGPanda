using System.Collections;
using UnityEngine;

using MultiplayerARPG;
using Panda;
using Panda.Examples.Shooter;

namespace RatherGood.Panda
{
    public partial class PandaBrain
    {

        [Category(2, "Combat", true)]

        [Space]
        [Header("Section: Combat")]

        [Tooltip("Random action state to do next time")]
        [SerializeField] BaseSkill queueSkill;
        [SerializeField] int queueSkillLevel;
        [SerializeField] bool isLeftHandAttacking;
        //[SerializeField] bool isWithinAttackRange;
        [SerializeField] bool attackInProcess;

        [SerializeField] float maxCombatMoveTargetTime = 5f;



        /// <summary>
        /// Do we have an active attack target?
        /// </summary>
        [Task]
        public bool Combat_TryGetTargetEntity()
        {

            if ((TargetEntity == null) || TargetEntity.Entity == Entity || TargetEntity.IsHideOrDead() || !TargetEntity.CanReceiveDamageFrom(Entity.GetInfo()))
            {
                // If target is dead or in safe area stop attacking
                Entity.SetTargetEntity(null);
                AiMode = aiModeDefault;
                return false;
            }

            return true;
        }


        [Task]
        public bool Combat_GetRandomSkillOrAttack()
        {

            // Random action state to do next time
            if (CharacterDatabase.RandomSkill(Entity, out queueSkill, out queueSkillLevel) && queueSkill != null)
            {
                if (Entity.IndexOfSkillUsage(queueSkill.DataId, SkillUsageType.Skill) >= 0)
                {
                    queueSkill = null;
                    queueSkillLevel = 0;
                    return false;// Cooling down
                }
            }

            return true;
        }



        float fleeTimeStartTimeStamp;

        /// <summary>
        /// Flee for min distance from enemy, adjust after time
        /// </summary>
        /// <param name="time"></param>
        /// <param name="istance"></param>
        /// <returns></returns>
        [Task]
        public void Combalt_FleeFromTarget(float minFleeDistance, float maxFleeTime)
        {

            if (TargetEntity == null)
                ThisTask.Succeed(); //Must be done

            float currentDistance = (TargetEntityPosition - EntityPosition).magnitude;

            if (currentDistance > minFleeDistance) //done
            {
                ThisTask.Succeed();
            }
            else if (ThisTask.isStarting)
            {

                Vector3 lookAwayDirection = (EntityPosition - TargetEntityPosition).normalized;

                float randomVariation = 5f;

                fleeTimeStartTimeStamp = Time.unscaledTime;

                for (int i = 0; i < 10; i++)
                {
                    //move minFleeDistance distance away form target
                    Vector3 tempNewDestination = TargetEntityPosition + (lookAwayDirection * (minFleeDistance + randomVariation));
                    Vector2 randomCircle = Random.insideUnitCircle * randomVariation;
                    tempNewDestination += new Vector3(randomCircle.x, 0f, randomCircle.y);

                    if (EntityMovement.FindGroundedPosition(tempNewDestination, 20, out tempNewDestination))
                    {
                        Movement_SetDestination(tempNewDestination);
                        Movement_MoveToDestination(ExtraMovementState.IsSprinting);
                        return; //ok moving
                    }
                }

                ThisTask.Fail(); //failed to find a position
            }
            else if ((Time.unscaledTime - fleeTimeStartTimeStamp) > maxFleeTime) //Failed to run away in time, readjust
            {
                ThisTask.Fail();
            }

            if (Task.isInspected)
                ThisTask.debugInfo = "Distance:" + currentDistance.ToString("N2") + " Time: " + (Time.unscaledTime - fleeTimeStartTimeStamp).ToString("N2");

        }

        float tryToChaseForCounter = 0f;

        [Task]
        public void Combat_MoveOnTargetUntilInRange(ExtraMovementState movementState)
        {

            if (TargetEntity == null)
            {
                ThisTask.Fail();
                return;
            }

            if (Combat_CanAttackEntityFromCurrentPosition())
            {
                Movement_StopMove();
                ThisTask.Succeed();
            }
            else if (ThisTask.isStarting)
            {
                tryToChaseForCounter = Time.unscaledTime;
            }
            else if ((Time.unscaledTime - tryToChaseForCounter) < maxCombatMoveTargetTime) //give up and try again time limit
            {
                Movement_SetDestination(TargetEntityPosition);
                Movement_MoveToDestination(ExtraMovementState.IsSprinting);
            }
            else //timout
            {
                Movement_StopMove();
                ThisTask.Fail();
            }

            if (Task.isInspected)
                ThisTask.debugInfo = "Distance:" + Vector3.Distance(EntityPosition, currentDestination).ToString("N2") + " Time: " + (Time.unscaledTime - tryToChaseForCounter).ToString("N2");

        }


        protected float GetAttackDistance()
        {
            return queueSkill != null && queueSkill.IsAttack ? queueSkill.GetCastDistance(Entity, queueSkillLevel, isLeftHandAttacking) :
                Entity.GetAttackDistance(isLeftHandAttacking);
        }


        //current weapon and current target
        [Task]
        bool Combat_CanAttackEntityFromCurrentPosition()
        {
            return Vector3.Distance(EntityPosition, TargetEntityPosition) < GetAttackDistance();
        }


        [Task]
        public void Combat_Attack()
        {
            Entity.AimPosition = Entity.GetAttackAimPosition(ref isLeftHandAttacking);

            if (!Combat_CanAttackEntityFromCurrentPosition()) //fail and reposition
            {
                ClearAttackStates();
                ThisTask.Fail();
            }
            else if (!IsPlayingActionAnimation)
            {

                if (queueSkill != null && Entity.IndexOfSkillUsage(queueSkill.DataId, SkillUsageType.Skill) < 0)
                {
                    attackInProcess = Entity.UseSkill(queueSkill.DataId, false, 0, new AimPosition()
                    {
                        type = AimPositionType.Position,
                        position = TargetEntity.OpponentAimTransform.position,
                    });
    
                }
                else
                {
                    // Attack when no queue skill
                    bool isLeftHand = false;
                    attackInProcess = Entity.Attack(ref isLeftHand);

                }

                ThisTask.Succeed();

            }

        }


        void ClearAttackStates()
        {
            isLeftHandAttacking = false;
            attackInProcess = false;
            queueSkill = null;
        }




    }
}