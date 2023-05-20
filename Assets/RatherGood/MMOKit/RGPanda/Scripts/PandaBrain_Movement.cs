using System.Collections;
using UnityEngine;

using MultiplayerARPG;
using Panda;
using Panda.Examples.Shooter;

namespace RatherGood.Panda
{
    public partial class PandaBrain
    {

        [Category(4, "Movement", true)]

        [Space]
        [Header("Section: Movement")]

        //[Tooltip("Turn to target speed. deg/ec")]
        //[SerializeField] float rotationSpeedNormal = 1f;

        //[Tooltip("Turn to enemy speed. deg/ec")]
        //[SerializeField] float rotationSpeedCombat = 10f;

        //[Tooltip("Turn speed while moving. Should be faster than standing still. deg/ec")]
        //[SerializeField] float rotationSpeedWhileMoving = 10f;

        [Tooltip("When setting a destination or moving, tolerance will be close enough to prevent running in circles.")]
        [SerializeField] float bufferTargetDistance = 0.25f;

        [Tooltip("When rotating towards target when to stop")]
        [SerializeField] float bufferRotateTowardsTargetYAngle = 5f;

        [SerializeField] Vector3 currentDestination; // The movement destination.

        [Tooltip("Turn to enemy speed")]
        [SerializeField] float turnToEnemySpeed = 800f;

        float DistanceToDestination => Vector3.Distance(Entity.EntityTransform.position, currentDestination);



        IEntityMovementComponent EntityMovement => Entity.Movement;

        [Task]
        public bool Movement_SetDestination(Vector3 targetDestination) //jsut set it here
        {
            currentDestination = targetDestination;
            return true;
        }


        [Task]
        public void Movement_MoveToDestination(ExtraMovementState movementState)
        {

            if (DistanceToDestination <= bufferTargetDistance)
            {
                Movement_StopMove();
                ThisTask.Succeed();
            }
            else
            {
                Entity.SetExtraMovementState(movementState);
                EntityMovement.FindGroundedPosition(currentDestination, 20, out currentDestination);
                EntityMovement.PointClickMovement(currentDestination);
            }

        }


        [Task]
        public bool Movement_StopMove()
        {
            currentDestination = EntityPosition; //current position
            EntityMovement.StopMove();
            EntityMovement.SetLookRotation(Entity.GetLookRotation()); //set to current
            return true;
        }



        [Task]
        public void Movement_SetLookRotationToLookTarget() //dont wait
        {
            if (TargetEntity == null)
            {
                ThisTask.Fail();
                return;
            }

            Vector3 lookAtDirection = (TargetEntityPosition - EntityPosition).normalized;
            Quaternion currentLookAtRotation = Entity.GetLookRotation();
            Vector3 currentLookRotationEuler = Quaternion.LookRotation(lookAtDirection).eulerAngles;

            float currentYAngleDifference = Mathf.Abs(currentLookAtRotation.eulerAngles.y - currentLookRotationEuler.y);

            if (currentYAngleDifference < 10f)
            {
                ThisTask.Succeed();
            }
            else
            {
                currentLookRotationEuler.x = 0;
                currentLookRotationEuler.z = 0;
                currentLookAtRotation = Quaternion.RotateTowards(currentLookAtRotation, Quaternion.Euler(currentLookRotationEuler), turnToEnemySpeed * Time.deltaTime);
                Entity.SetLookRotation(currentLookAtRotation);
            }

            if (Task.isInspected)
                ThisTask.debugInfo = "Rotation Diff: " + currentLookAtRotation.ToString("N2");
        }

        #region Waypoints


        [Tooltip("Example Panda waypoint transform list")]

        [SerializeField] WaypointPath waypointPath;

        [SerializeField] int waypointIndex;

        int WaypointIndex
        {
            get { return waypointIndex; }
            set
            {
                int tempIindx = value;

                while (tempIindx < 0) { tempIindx += waypointPath.waypoints.Length; }
                if (tempIindx >= waypointPath.waypoints.Length) { tempIindx %= waypointPath.waypoints.Length; }

                waypointIndex = tempIindx;

            }
        }

        [Task]
        public bool Movement_SetNextWaypointIndex()
        {
            if (waypointPath != null)
            {
                WaypointIndex++;
                if (Task.isInspected)
                    ThisTask.debugInfo = string.Format("i={0}", WaypointIndex);
                Movement_SetDestinationToWaypoint(WaypointIndex);
            }
            return true;
        }


        [Task]
        public bool Movement_SetDestinationToWaypoint(int i)
        {
            bool isSet = false;
            if (waypointPath != null)
            {
                isSet = Movement_SetDestination(waypointPath.waypoints[i].position);
            }
            return isSet;
        }

        #endregion Waypoints

        [Task]
        public void Movement_FolowSummoner()
        {
            if (Entity.Summoner == null)
            {
                ThisTask.Fail();
                return;
            }

            float distToSummoner = Vector3.Distance(EntityPosition, Entity.Summoner.EntityTransform.position);

            if ((distToSummoner > GameInstance.Singleton.minFollowSummonerDistance) &&
                 (distToSummoner < GameInstance.Singleton.maxSummonDistance))
            {
                Movement_StopMove();
                ThisTask.Succeed();
            }
            else
            {
                if (FindUnobstructedPositionNearTarget(Entity.Summoner.EntityTransform.position, out Vector3 newCurrentDestination, GameInstance.Singleton.minFollowSummonerDistance * 1.1f, GameInstance.Singleton.maxSummonDistance * 0.95f))
                {
                    currentDestination = newCurrentDestination; //continue following
                    Entity.ActiveMovement.PointClickMovement(currentDestination);
                }
                else
                {
                    Movement_StopMove();
                    ThisTask.Fail(); //Couldnt find a position
                }

            }


        }

        [Task]
        public bool Movement_SetRandomMoveTargetAroundSpawnPosition(float radius)
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            currentDestination = Entity.SpawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
            return true;
        }


        [Task]
        public void Movement_SetRandomMoveTargetAroundCurrentPosition(float radius)
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            currentDestination = Entity.MovementTransform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
        }


        [Task]
        private bool Movement_IsMoving()
        {
            return (!Entity.MovementState.HasFlag(MovementState.IsGrounded) ||
                    Entity.MovementState.HasFlag(MovementState.Forward) ||
                    Entity.MovementState.HasFlag(MovementState.Backward) ||
                    Entity.MovementState.HasFlag(MovementState.Left) ||
                    Entity.MovementState.HasFlag(MovementState.Right) ||
                    Entity.MovementState.HasFlag(MovementState.IsJump));
        }

        [Header("Find poaition")]

        [SerializeField] int maxFindPositionTries = 10;

        [SerializeField] float randomVariationAroundPosition = 2f;


        /// <summary>
        /// Find Unobstructed position near/towards a target position
        /// Just return current position if its already good first.
        /// Follow summoner or target position
        /// </summary>
        /// <param name="targetEntity"></param>
        /// <param name="newLocation"></param>
        /// <param name="minApproachDistance"></param>
        /// <param name="maxApproachDistance"></param>
        /// <returns> true if valid location found. </returns>
        public bool FindUnobstructedPositionNearTarget(Vector3 targetPosition, out Vector3 newLocation, float minApproachDistance = 0, float maxApproachDistance = 0)
        {

            Vector3 myPosition = EntityPosition;

            newLocation = myPosition;

            //return first strait path to target
            Vector3 tempNewLocation = RestrictDistance(myPosition, targetPosition, minApproachDistance, maxApproachDistance);

            //Now test if this new point can walk on
            if (Entity.ActiveMovement.FindGroundedPosition(tempNewLocation, 20, out tempNewLocation))
            {
                newLocation = tempNewLocation;
                return true; //this spot is good
            }

            //try to get a valid grounded position
            for (int i = 0; i < maxFindPositionTries; i++)
            {

                //randomize around target
                Vector2 rnd = (Random.insideUnitCircle * randomVariationAroundPosition);

                tempNewLocation = targetPosition + new Vector3(rnd.x, 0, rnd.y);

                //verify the new random location is in valid range
                tempNewLocation = RestrictDistance(myPosition, tempNewLocation, minApproachDistance, maxApproachDistance);

                //Now test if this new point can walk on
                if (Entity.ActiveMovement.FindGroundedPosition(tempNewLocation, 20, out tempNewLocation))
                {
                    newLocation = tempNewLocation;
                    return true; //this spot is good
                }

            }

            return false; //failed to obtain a good position

        }

        /// <summary>
        /// Restrict a vector3 position facing a target from min to max. 
        /// If too close will return a position at min distance.
        /// If too far will return closer position at max distance.
        /// </summary>
        /// <param name="startPosition"></param>
        /// <param name="targetPosition"></param>
        /// <param name="minApproachDistance"></param>
        /// <param name="maxApproachDistance"></param>
        /// <returns></returns>
        public static Vector3 RestrictDistance(Vector3 startPosition, Vector3 targetPosition, float minDistance, float maxDistance)
        {

            float currentDistance = (targetPosition - startPosition).magnitude;

            Vector3 direction = (targetPosition - startPosition).normalized;

            if (currentDistance < minDistance) //must be too close, back up! 
            {
                return (targetPosition - (direction * minDistance));
            }
            else if (currentDistance > maxDistance) //too far, bring it in
            {
                return (targetPosition - (direction * maxDistance));
            }

            return startPosition; //All good!
        }

    }
}