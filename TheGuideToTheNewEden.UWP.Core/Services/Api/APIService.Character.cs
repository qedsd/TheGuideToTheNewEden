using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 角色技能
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterSkills(GameServerType server,int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/skills/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/skills/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 角色技能
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterSkills(int characterId, string token)=> CharacterSkills(DefaultGameServer,characterId,token);

        /// <summary>
        /// 角色钱包
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterWallet(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/wallet/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/wallet/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 角色钱包
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterWallet(int characterId, string token)=> CharacterWallet(DefaultGameServer, characterId,token);

        /// <summary>
        /// 角色忠诚点数
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterLoyaltyPoint(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/loyalty/points/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/loyalty/points/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 角色忠诚点数
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterLoyaltyPoint(int characterId, string token)=> CharacterLoyaltyPoint(DefaultGameServer,characterId, token);

        /// <summary>
        /// 在线情况
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterOnline(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/online/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/online/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 在线情况
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterOnline(int characterId, string token)=> CharacterOnline(DefaultGameServer,characterId,token);

        /// <summary>
        /// 当前位置
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterLocation(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/location/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/location/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 当前位置
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterLocation(int characterId, string token)=> CharacterLocation(DefaultGameServer, characterId, token);

        /// <summary>
        /// 当前舰船信息
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterShip(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/ship/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/ship/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 当前舰船信息
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterShip(int characterId, string token)=> CharacterShip(DefaultGameServer,characterId, token);

        /// <summary>
        /// 技能队列
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterSkillqueue(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/skillqueue/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/skillqueue/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 技能队列
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterSkillqueue(int characterId, string token) => CharacterSkillqueue(DefaultGameServer, characterId, token);

        /// <summary>
        /// 钱包记录
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterWalletJournal(GameServerType server, int characterId, string token, int page)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/wallet/journal/?datasource=tranquility&token={token}&page={page}";
            else
                return $"{SerenityUri}/characters/{characterId}/wallet/journal/?datasource=serenity&token={token}&page={page}";
        }
        /// <summary>
        /// 钱包记录
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public static string CharacterWalletJournal(int characterId, string token, int page)=> CharacterWalletJournal(DefaultGameServer,characterId,token,page);

        /// <summary>
        /// 交易记录
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterWalletTransactions(GameServerType server, int characterId, string token, int page)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/wallet/transactions/?datasource=tranquility&token={token}&page={page}";
            else
                return $"{SerenityUri}/characters/{characterId}/wallet/transactions/?datasource=serenity&token={token}&page={page}";
        }
        /// <summary>
        /// 交易记录
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public static string CharacterWalletTransactions(int characterId, string token, int page)=> CharacterWalletTransactions(DefaultGameServer,characterId:characterId,token:token,page);

        /// <summary>
        /// 邮箱标签
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterMailLabels(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/mail/labels/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/mail/labels/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 邮箱标签
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterMailLabels(int characterId, string token)=> CharacterMailLabels(DefaultGameServer,characterId,token);

        /// <summary>
        /// 订阅邮件列表
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterMailLists(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/mail/lists/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/mail/lists/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 订阅邮件列表
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterMailLists(int characterId, string token)=> CharacterMailLists(DefaultGameServer, characterId,token);

        /// <summary>
        /// 最近50邮件
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterMail(GameServerType server, int characterId, string token,int lastMailId = 0)
        {
            string baseUri = server == GameServerType.Tranquility ? TranquilityUri : SerenityUri;
            if (lastMailId == 0)
            {
                return $"{baseUri}/characters/{characterId}/mail/?datasource={server.ToString().ToLower()}&token={token}";
            }
            else
            {
                return $"{baseUri}/characters/{characterId}/mail/?datasource={server.ToString().ToLower()}&token={token}&last_mail_id={lastMailId}";
            }
        }
        /// <summary>
        /// 最近50邮件
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterMail(int characterId, string token,int lastMailId)=> CharacterMail(DefaultGameServer,characterId, token, lastMailId);

        /// <summary>
        /// 指定邮箱标签最近50邮件
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterMailOfLables(GameServerType server, int characterId, string token, List<int> labelIds, int lastMailId = 0)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach(var item in labelIds)
            {
                stringBuilder.Append(item.ToString());
                stringBuilder.Append(',');
            }
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            string baseUri = server == GameServerType.Tranquility ? TranquilityUri : SerenityUri;
            if (lastMailId == 0)
                return $"{baseUri}/characters/{characterId}/mail/?datasource={server.ToString().ToLower()}& token={token}&labels={stringBuilder}";
            else
                return $"{baseUri}/characters/{characterId}/mail/?datasource={server.ToString().ToLower()}& token={token}&labels={stringBuilder}&last_mail_id={lastMailId}";
        }
        /// <summary>
        /// 指定邮箱标签最近50邮件
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <param name="labelIds"></param>
        /// <returns></returns>
        public static string CharacterMailOfLables(int characterId, string token, List<int> labelIds, int lastMailId = 0) => CharacterMailOfLables(DefaultGameServer,characterId,token,labelIds, lastMailId);

        /// <summary>
        /// 指定id邮件详情
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterMailDetail(GameServerType server, int characterId, string token, int id)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/mail/{id}/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/mail/{id}/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 指定id邮件详情
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string CharacterMailDetail(int characterId, string token, int id) => CharacterMailDetail(DefaultGameServer, characterId,token,id);

        /// <summary>
        /// 指定页数合同列表
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterContracts(GameServerType server, int characterId, string token, int page)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/contracts/?datasource=tranquility&token={token}&page={page}";
            else
                return $"{SerenityUri}/characters/{characterId}/contracts/?datasource=serenity&token={token}&page={page}";
        }
        /// <summary>
        /// 指定页数合同列表
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public static string CharacterContracts(int characterId, string token, int page)=> CharacterContracts(DefaultGameServer,characterId, token,page);

        /// <summary>
        /// 指定合同详细
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterContractDetail(GameServerType server, int characterId, string token, int id)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/contracts/{id}/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/contracts/{id}/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 指定合同详细
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string CharacterContractDetail(int characterId, string token, int id) => CharacterContractDetail(DefaultGameServer,characterId,token,id);

        /// <summary>
        /// 拍卖合同出价情况
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterContractBids(GameServerType server, int characterId, string token, int id)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/contracts/{id}/bids/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/contracts/{id}/bids/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 拍卖合同出价情况
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string CharacterContractBids(int characterId, string token, int id) => CharacterContractBids(DefaultGameServer, characterId,token,id);

        /// <summary>
        /// 克隆体信息
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterClones(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/clones/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/clones/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 克隆体信息
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterClones(int characterId, string token) => CharacterClones(DefaultGameServer,characterId, token);

        /// <summary>
        /// 当前克隆体植入体
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterImplants(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/implants/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/implants/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 当前克隆体植入体
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterImplants(int characterId, string token) => CharacterImplants(DefaultGameServer,characterId,token);

        /// <summary>
        /// 军团、联盟信息
        /// post
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterAffiliation(GameServerType server)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/affiliation/?datasource=tranquility";
            else
                return $"{SerenityUri}/characters/affiliation/?datasource=serenity";
        }
        /// <summary>
        ///  军团、联盟信息
        ///  post
        /// </summary>
        /// <returns></returns>
        public static string CharacterAffiliation() => CharacterAffiliation(DefaultGameServer);

        /// <summary>
        /// 角色基本信息
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterInfo(GameServerType server, int characterId)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/?datasource=tranquility";
            else
                return $"{SerenityUri}/characters/{characterId}/?datasource=serenity";
        }
        /// <summary>
        /// 角色基本信息
        /// </summary>
        /// <param name="characterId"></param>
        /// <returns></returns>
        public static string CharacterInfo(int characterId) => CharacterInfo(DefaultGameServer,characterId);

        /// <summary>
        /// 市场订单
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterOrders(GameServerType server, int characterId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/orders/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/characters/{characterId}/orders/?datasource=serenity&token={token}";
        }
        /// <summary>
        /// 市场订单
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterOrders(int characterId, string token) => CharacterOrders(DefaultGameServer,characterId,token);

        /// <summary>
        /// 资产
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterOrders(GameServerType server, int characterId, string token, int page)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/assets/?datasource=tranquility&token={token}&page={page}";
            else
                return $"{SerenityUri}/characters/{characterId}/assets/?datasource=serenity&token={token}&page={page}";
        }
        /// <summary>
        /// 资产
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public static string CharacterOrders(int characterId, string token, int page) => CharacterOrders(DefaultGameServer,characterId,token,page);

        /// <summary>
        /// 雇佣历史
        /// </summary>
        /// <param name="server"></param>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string CharacterCorporationhistory(GameServerType server, int characterId)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/characters/{characterId}/corporationhistory/?datasource=tranquility";
            else
                return $"{SerenityUri}/characters/{characterId}/corporationhistory/?datasource=serenity";
        }
        /// <summary>
        /// 雇佣历史
        /// </summary>
        /// <param name="characterId"></param>
        /// <returns></returns>
        public static string CharacterCorporationhistory(int characterId) => CharacterCorporationhistory(DefaultGameServer,characterId);
    }
}
