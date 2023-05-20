using System.Collections;
using UnityEngine;



namespace MultiplayerARPG
{
    
    public abstract partial class BaseMonsterCharacterEntity
    {
        public void SetSummoner(BaseCharacterEntity setSummoner)
        {
            Summoner = setSummoner;
        }

    }
}