using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.CharacterScan
{
    public class CharacterScanInfo
    {
        public int Id { get => Character.Id;}
        public string Name { get => Character.Name; }
        public IdName Character { get; set; }
        public IdName Corporation { get; set; }
        public IdName Alliance { get; set; }
        public IdName Faction { get; set; }

        public static CharacterScanInfo Create(int characterId, int corporationId, int allianceId)
        {
            List<int> ids = new List<int>();
            if(characterId > 0) ids.Add(characterId);
            if(corporationId > 0) ids.Add(corporationId);
            if(allianceId > 0) ids.Add(allianceId);
            var names = Core.Services.IDNameService.GetByIds(ids);
            IdName character = names.FirstOrDefault(p => p.Id == characterId);
            character = character ?? new IdName(characterId, characterId.ToString(), IdName.CategoryEnum.Character);

            IdName corp = names.FirstOrDefault(p => p.Id == corporationId);
            corp = corp ?? new IdName(corporationId, corporationId.ToString(), IdName.CategoryEnum.Corporation);

            IdName alliance = null;
            if(allianceId > 0)
            {
                alliance = names.FirstOrDefault(p => p.Id == allianceId);
                alliance = alliance ?? new IdName(allianceId, allianceId.ToString(), IdName.CategoryEnum.Alliance);
            }
            else
            {
                alliance = new IdName(allianceId, "No alliance", IdName.CategoryEnum.Alliance);
            }

            CharacterScanInfo characterScanInfo = new CharacterScanInfo()
            {
                Character = character,
                Corporation = corp,
                Alliance = alliance,
            };
            return characterScanInfo;
        }

        public bool GetZKBInfo()
        {
            return true;
        }
    }
}
