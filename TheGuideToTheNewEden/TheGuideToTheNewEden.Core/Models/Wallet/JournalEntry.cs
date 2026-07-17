using System;
using System.Collections.Generic;
using System.Text;
using EVEStandard.Enumerations;
using System.Text.Json.Serialization;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Wallet
{
    public class JournalEntry : EVEStandard.Models.CharacterWalletJournal
    {
        #region Properties

        /// <summary>
        /// Transaction amount. Positive when value transferred to the first party. Negative otherwise
        /// </summary>
        /// <value>Transaction amount. Positive when value transferred to the first party. Negative otherwise</value>
        [JsonPropertyName("amount")]
        public double? Amount { get; set; }

        /// <summary>
        /// Wallet balance after transaction occurred
        /// </summary>
        /// <value>Wallet balance after transaction occurred</value>
        [JsonPropertyName("balance")]
        public double? Balance { get; set; }

        /// <summary>
        /// Gets or sets the context identifier.
        /// </summary>
        /// <value>
        /// The context identifier.
        /// </value>
        [JsonPropertyName("context_id")]
        public long ContextId { get; set; }

        /// <summary>
        /// Gets or sets the type of the context identifier.
        /// </summary>
        /// <value>
        /// The type of the context identifier.
        /// </value>

        [JsonPropertyName("context_id_type")]

        public string ContextIdType { get; set; }

        /// <summary>

        /// Gets the ContextIdType as enum (may throw exception if unknown value exists).

        /// </summary>

        [Obsolete("This property will be removed in a future version. Use the string property instead and parse manually if needed.")]

        [JsonIgnore]

        public ContextType ContextIdTypeToEnum

        {

            get => (ContextType)Enum.Parse(typeof(ContextType), ContextIdType);

        }

        /// <summary>
        /// Date and time of transaction
        /// </summary>
        /// <value>Date and time of transaction</value>
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>
        /// The description.
        /// </value>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// first_party_id integer
        /// </summary>
        /// <value>first_party_id integer</value>
        [JsonPropertyName("first_party_id")]
        public long? FirstPartyId { get; set; }

        /// <summary>
        /// Unique journal reference ID
        /// </summary>
        /// <value>Unique journal reference ID</value>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// reason string
        /// </summary>
        /// <value>reason string</value>
        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        /// <summary>
        /// Transaction type, different type of transaction will populate different fields in &#x60;extra_info&#x60; Note: If you have an existing XML API application that is using ref_types, you will need to know which string ESI ref_type maps to which integer. You can use the following gist to see string-&gt;int mappings: https://gist.github.com/ccp-zoetrope/c03db66d90c2148724c06171bc52e0ec
        /// </summary>
        /// <value>Transaction type, different type of transaction will populate different fields in &#x60;extra_info&#x60; Note: If you have an existing XML API application that is using ref_types, you will need to know which string ESI ref_type maps to which integer. You can use the following gist to see string-&gt;int mappings: https://gist.github.com/ccp-zoetrope/c03db66d90c2148724c06171bc52e0ec</value>

        [JsonPropertyName("ref_type")]

        public string RefType { get; set; }

        /// <summary>

        /// Gets the RefType as enum (may throw exception if unknown value exists).

        /// </summary>

        [Obsolete("This property will be removed in a future version. Use the string property instead and parse manually if needed.")]

        [JsonIgnore]

        public TransactionType RefTypeToEnum

        {

            get => (TransactionType)Enum.Parse(typeof(TransactionType), RefType);

        }

        /// <summary>
        /// second_party_id integer
        /// </summary>
        /// <value>second_party_id integer</value>
        [JsonPropertyName("second_party_id")]
        public long? SecondPartyId { get; set; }

        /// <summary>
        /// Tax amount received for tax related transactions
        /// </summary>
        /// <value>Tax amount received for tax related transactions</value>
        [JsonPropertyName("tax")]
        public double? Tax { get; set; }

        /// <summary>
        /// the corporation ID receiving any tax paid
        /// </summary>
        /// <value>the corporation ID receiving any tax paid</value>
        [JsonPropertyName("tax_receiver_id")]
        public long? TaxReceiverId { get; set; }

        #endregion Properties
        public JournalEntry(EVEStandard.Models.CharacterWalletJournal journalEntry) 
        {
            this.CopyFrom(journalEntry);
        }
        public JournalEntry(EVEStandard.Models.CorporationWalletJournal journalEntry)
        {
            this.CopyFrom(journalEntry);
        }

        public string AmountStr
        {
            get
            {
                var str = Amount.Value.ToString("N2");
                if(str != null && str[0] != '-')
                {
                    return '+' + str;
                }
                else
                {
                    return str;
                }
            }
        }
    }
}
