using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Contract
{
    public class ContractInfo
    {
        public enum TypeEnum
        {
            unknown = 1,
            item_exchange = 2,
            auction = 3,
            courier = 4,
            loan = 5,
        }

        public long AcceptorId { get; set; }
        public long AssigneeId { get; set; }
        public string Availability { get; set; }
        public double? Buyout { get; set; }
        public double? Collateral { get; set; }
        public long ContractId { get; set; }
        public DateTime? DateAccepted { get; set; }
        public DateTime? DateCompleted { get; set; }
        public DateTime DateExpired { get; set; }
        public DateTime DateIssued { get; set; }
        public long? DaysToComplete { get; set; }
        public long? EndLocationId { get; set; }
        public bool ForCorporation { get; set; }
        public long IssuerCorporationId { get; set; }
        public long IssuerId { get; set; }
        public double? Price { get; set; }
        public double? Reward { get; set; }
        public long? StartLocationId { get; set; }
        public string Status { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public double? Volume { get; set; }

        // 自定义属性（从外部数据补充）
        public string IssuerName { get; set; }
        public string AssigneeName { get; set; }
        public string AcceptorName { get; set; }
        public string StartLocationName { get; set; }
        public string EndLocationName { get; set; }
        public string TypeStr { get; set; }

        public ContractInfo() { }

        public ContractInfo(EVEStandard.Models.Contract contract)
        {
            AcceptorId = contract.AcceptorId;
            AssigneeId = contract.AssigneeId;
            Availability = contract.Availability;
            Buyout = contract.Buyout;
            Collateral = contract.Collateral;
            ContractId = contract.ContractId;
            DateAccepted = contract.DateAccepted;
            DateCompleted = contract.DateCompleted;
            DateExpired = contract.DateExpired;
            DateIssued = contract.DateIssued;
            DaysToComplete = contract.DaysToComplete;
            EndLocationId = contract.EndLocationId;
            ForCorporation = contract.ForCorporation;
            IssuerCorporationId = contract.IssuerCorporationId;
            IssuerId = contract.IssuerId;
            Price = contract.Price;
            Reward = contract.Reward;
            StartLocationId = contract.StartLocationId;
            Status = contract.Status;
            Title = contract.Title;
            Type = contract.Type;
            Volume = contract.Volume;
        }

        public TypeEnum GetTypeEnum()
        {
            if (Enum.TryParse<TypeEnum>(Type, ignoreCase: true, out var result))
                return result;
            return TypeEnum.unknown;
        }
    }
}
