using System.Collections;
using UnityEngine;

using MultiplayerARPG;
using Panda;
using System.Collections.Generic;

namespace RatherGood.Panda
{
    [RequireComponent(typeof(PandaBehaviour))]
    public partial class PandaBrain : BaseGameEntityComponent<BaseMonsterCharacterEntity> //: MonsterActivityComponent //
    {


        [Category(1, "Main Stuff", true)]

        public bool enableAIBrainUpdate = true;

        [SerializeField] AIModeEnum aiMode = AIModeEnum.none;

        public AIModeEnum AiMode { get => aiMode; set => aiMode = value; }

        [SerializeField] AIModeEnum aiModeDefault = AIModeEnum.patrol;

        [Task]
        public bool AiModeCompare(AIModeEnum isMode)//Check if this mode is active in tree
        {
            return AiMode == isMode;
        }

        [Task] public bool IsServer => Entity.IsServer;

        [Task] public bool IsDead => Entity.IsDead();

        [Task] public bool IsAlive => !IsDead;

        PandaBehaviour pandaB;

        public MonsterCharacter CharacterDatabase => Entity.CharacterDatabase;

        public bool IsPlayingActionAnimation => Entity.IsPlayingActionAnimation();

        Vector3 TargetEntityPosition => (TargetEntity != null) ? TargetEntity.GetTransform().position : EntityPosition;

        Vector3 EntityPosition => Entity.EntityTransform.position;

        public override void EntityAwake()
        {
            base.EntityAwake();

            pandaB = GetComponent<PandaBehaviour>();
            pandaB.tickOn = BehaviourTree.UpdateOrder.Manual;

            Entity.onNotifyEnemySpotted += Entity_onNotifyEnemySpotted;
            Entity.onNotifyEnemySpottedByAlly += Entity_onNotifyEnemySpottedByAlly;
            Entity.onReceivedDamage += Entity_onReceivedDamage;

        }

        public override void EntityOnDestroy()
        {
            base.EntityOnDestroy();
            Entity.onNotifyEnemySpotted -= Entity_onNotifyEnemySpotted;
            Entity.onNotifyEnemySpottedByAlly -= Entity_onNotifyEnemySpottedByAlly;
            Entity.onReceivedDamage -= Entity_onReceivedDamage;
        }


        public override void EntityStart()
        {
            base.EntityStart();

            pandaB.sourceInfos = null;
            pandaB.Apply(); //will set _btSources = null;
            pandaB.Compile();

        }


        public override void EntityUpdate()
        {
            base.EntityUpdate();

            //only update from server and if palyers are nearby?
            if (!Entity.IsServer || Entity.Identity.CountSubscribers() == 0)
                return;

            if (!enableAIBrainUpdate)
                return;

            if (!Entity || IsDead)
            {
                aiMode = aiModeDefault;
                return;
            }

            pandaB.Tick(); //Manual tick update Panda with entity

        }


        private void Entity_onNotifyEnemySpotted(BaseCharacterEntity enemy)
        {
            if (Entity.Characteristic != MonsterCharacteristic.Assist)
                return;
            // Warn that this character received damage to nearby characters
            List<BaseCharacterEntity> foundCharacters = Entity.FindAliveEntities<BaseCharacterEntity>(CharacterDatabase.VisualRange, true, false, false, GameInstance.Singleton.playerLayer.Mask | GameInstance.Singleton.playingLayer.Mask | GameInstance.Singleton.monsterLayer.Mask);
            if (foundCharacters == null || foundCharacters.Count == 0) return;
            foreach (BaseCharacterEntity foundCharacter in foundCharacters)
            {
                foundCharacter.NotifyEnemySpottedByAlly(Entity, enemy);
            }
        }

        private void Entity_onNotifyEnemySpottedByAlly(BaseCharacterEntity ally, BaseCharacterEntity enemy)
        {
            if ((Entity.Summoner != null && Entity.Summoner == ally) ||
                Entity.Characteristic == MonsterCharacteristic.Assist)
            {
                TargetEntity = enemy;
                AiMode = AIModeEnum.combat;
            }

        }

        private void Entity_onReceivedDamage(HitBoxPosition position, Vector3 fromPosition, IGameEntity attacker, CombatAmountType combatAmountType, int totalDamage, CharacterItem weapon, BaseSkill skill, int skillLevel, CharacterBuff buff, bool isDamageOverTime)
        {
            BaseCharacterEntity attackerCharacter = attacker as BaseCharacterEntity;
            if (attackerCharacter == null)
                return;

            // If character is not dead, try to attack
            if (!Entity.IsDead())
            {
                if (Entity.GetTargetEntity() == null)
                {
                    // If no target enemy, set target enemy as attacker
                    TargetEntity = attackerCharacter;
                    AiMode = AIModeEnum.combat;
                }

            }
        }


        public class CounterFloatInfoRG
        {
            public float startTime;
        }

        [SerializeField] BaseCharacterEntity debugTargetEntity;

        public BaseCharacterEntity TargetEntity
        {
            get { return (BaseCharacterEntity)Entity.GetTargetEntity(); }
            set
            {
                debugTargetEntity = value;
                Entity.SetTargetEntity(value);
            }
        }

        #region Tasks



        [Task]
        public void WaitRealTimeRG(float seconds)
        {

            var info = ThisTask.data != null ? (CounterFloatInfoRG)ThisTask.data : (CounterFloatInfoRG)(ThisTask.data = new CounterFloatInfoRG());

            float elapsedTime = 0;

            if (ThisTask.isStarting)
            {
                info.startTime = Time.unscaledTime;
            }
            else
            {
                elapsedTime = Time.unscaledTime - info.startTime;
            }

            if (Task.isInspected)
            {
                float tta = Mathf.Clamp(elapsedTime, 0.0f, float.PositiveInfinity);
                ThisTask.debugInfo = string.Format("t-{0:0.000}", tta);
            }

            if (elapsedTime >= seconds)
            {
                ThisTask.debugInfo = string.Format("t-{0:0.000}", elapsedTime);
                ThisTask.Succeed();
            }

        }

        #endregion Tasks

        [SerializeField] BaseCharacterEntity setSummoner;

        [InspectorButton(nameof(DebugSetSummoner))]
        [SerializeField] bool debugSetSummoner = false;
        void DebugSetSummoner()
        {
            SetSummoner(setSummoner);
        }


        void SetSummoner(BaseCharacterEntity summoner)
        {
            Entity.SetSummoner(summoner);
        }

    }


    /// <summary>
    /// sorted by priority
    /// </summary>
    [System.Serializable]
    public enum AIModeEnum
    {
        none, //lowest priority
        idle,
        patrol,
        //greeting, //greeting (or actions) on first encounter with another entity
        //interacting, //conversation or trade
        alert,
        combat,
        fleeing, //highest priority
        followSummoner,

    }
}