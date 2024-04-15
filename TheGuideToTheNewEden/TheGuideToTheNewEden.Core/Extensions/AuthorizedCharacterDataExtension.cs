using ESI.NET;
using ESI.NET.Models.SSO;
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
        public static bool IsTokenValid(this AuthorizedCharacterData data)
        {
            return data.ExpiresOn.ToLocalTime() > DateTime.Now;
        }
        /// <summary>
        /// 刷新token
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static async Task<bool> RefreshTokenAsync(this AuthorizedCharacterData data)
        {
            try
            {
                var token = await ESIService.GetToken(ESI.NET.Enumerations.GrantType.RefreshToken, data.RefreshToken, Guid.NewGuid().ToString());
                if (token != null)
                {
                    var newdata = await ESIService.Verify(token);
                    if (newdata != null)
                    {
                        data.CopyFrom(newdata);
                        return true;
                    }
                    else
                    {
                        Log.Error("刷新Token时Verify返回空值");
                        return false;
                    }
                }
                else
                {
                    Log.Error("刷新Token时GetToken返回空值");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return false;
            }
        }
    }
}
