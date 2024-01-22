using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class EntityBaseInfo:ObservableObject
    {
        private IdName characterName;
        public IdName CharacterName{ get => characterName; set => SetProperty(ref characterName, value); }

        private IdName corpName;
        public IdName CorpName { get => corpName; set => SetProperty(ref corpName, value); }

        private IdName ceoName;
        public IdName CEOName { get => ceoName; set => SetProperty(ref ceoName, value); }

        private IdName allianceName;
        public IdName AllianceName { get => allianceName; set => SetProperty(ref allianceName, value); }

        private IdName executorCorpName;
        public IdName ExecutorCorpName { get => executorCorpName; set => SetProperty(ref executorCorpName, value); }

        private IdName systemName;
        public IdName SystemName { get => systemName; set => SetProperty(ref systemName, value); }

        private IdName constellationName;
        public IdName ConstellationName { get => constellationName; set => SetProperty(ref constellationName, value); }

        private IdName regionName;
        public IdName RegionName { get => regionName; set => SetProperty(ref regionName, value); }

        private IdName shipName;
        public IdName ShipName { get => shipName; set => SetProperty(ref shipName, value); }

        private IdName className;
        public IdName ClassName { get => className; set => SetProperty(ref className, value); }

        private IdName factionName;
        public IdName FactionName { get => factionName; set => SetProperty(ref factionName, value); }

        private int? members;
        public int? Members { get => members; set => SetProperty(ref members, value); }
        private float? sec;
        public float? Sec { get => sec; set => SetProperty(ref sec, value); }

    }
}
