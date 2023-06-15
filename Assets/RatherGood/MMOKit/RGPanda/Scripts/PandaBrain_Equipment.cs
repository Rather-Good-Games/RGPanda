using System.Collections;
using UnityEngine;

using MultiplayerARPG;
using Panda;
using Panda.Examples.Shooter;

namespace RatherGood.Panda
{
    public partial class PandaBrain
    {
        [Category(3, "Equipment", true)]

        [Space]
        [Header("Section: Equipment")]

        [SerializeField] bool equipPlaceholder;

        [Task] bool IsWeaponsSheathed => Entity.IsWeaponsSheathed;


        [Task]
        bool SheathWeapons(bool sheath)
        {
            Entity.IsWeaponsSheathed = sheath;
            return true;
        }



    }
}