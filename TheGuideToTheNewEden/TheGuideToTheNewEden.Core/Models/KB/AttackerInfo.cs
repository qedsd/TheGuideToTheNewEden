using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using ZKB.NET.Models.Killmails;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class AttackerInfo
    {
        public Attacker Attacker { get; set; }
        public InvType Ship {  get; set; }
        public InvType Weapon { get; set; }
        public IdName CharacterName { get; set; }
        public IdName CorpName { get; set; }
        public IdName AllianceName { get; set; }
        public float DamageRatio { get; set; }
        public AttackerInfo(Attacker attacker)
        {
            Attacker = attacker;
        }
    }
}
