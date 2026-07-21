using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.Core.Extensions
{
    public static class AuthorizedCharacterDataExtension
    {
        /// <summary>
        /// 检查是否可用（未过期）
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool IsTokenValid(this Models.Character.AuthorizedCharacterData data)
        {
            return data.ExpiresOn.ToLocalTime() > DateTime.Now;
        }
        /// <summary>
        /// 刷新token
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static async Task<bool> RefreshTokenAsync(this Models.Character.AuthorizedCharacterData data)
        {
            try
            {
                return await ESIService.Current.Refresh(data);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return false;
            }
        }
    }
}
