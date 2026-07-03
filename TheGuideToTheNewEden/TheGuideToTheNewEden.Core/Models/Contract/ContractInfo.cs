using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Contract
{
    public class ContractInfo: EVEStandard.Models.Contract
    {
        public string IssuerName { get; set; }
        public string AssigneeName { get; set; }
        public string AcceptorName { get; set; }
        public string StartLocationName { get; set; }
        public string EndLocationName { get; set; }
        public string TypeStr { get; set; }
        public ContractInfo(EVEStandard.Models.Contract contract)
        {
            this.CopyFrom(contract);
        }
        public EVEStandard.Models.Contract.TypeEnum GetTypeEnum()
        {
            return Enum.Parse<EVEStandard.Models.Contract.TypeEnum>(Type);
        }
    }
}
