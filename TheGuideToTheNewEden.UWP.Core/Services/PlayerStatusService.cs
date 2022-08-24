using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.PlayerStatus;

namespace TheGuideToTheNewEden.Core.Services
{
    public static class PlayerStatusService
    {
        

        private static string Api { get => Config.PlayerStatusApi; }

        public static async Task<IEnumerable<TiebaStatusPerMonth>> GetTiebaStatusPerMonthAsync(int year, int serverType)
        {
            string result = await Helpers.HttpHelper.GetAsync($"{Api}/newedenapi/status/tieba/year?ServerType={serverType}&&Year={year}");
            return JsonConvert.DeserializeObject<IEnumerable<TiebaStatusPerMonth>>(result);
        }
        private static async Task<IEnumerable<TiebaStatusPerDay>> GetTiebaStatusPerDayAsync(DateTime date, int serverType)
        {
            string result = await Helpers.HttpHelper.GetAsync($"{Api}/newedenapi/status/tieba/month?ServerType={serverType}&&Date={date}");
            return JsonConvert.DeserializeObject<IEnumerable<TiebaStatusPerDay>>(result);
        }
        private static async Task<IEnumerable<ServerStatusPerMonth>> GetServerStatusPerMonthAsync(int year, int serverType)
        {
            string result = await Helpers.HttpHelper.GetAsync($"{Api}/newedenapi/status/game/year?ServerType={serverType}&&Year={year}");
            return JsonConvert.DeserializeObject<IEnumerable<ServerStatusPerMonth>>(result);
        }

        private static async Task<IEnumerable<ServerStatusPerDay>> GetServerStatusPerDayAsync(DateTime date, int serverType)
        {
            string result = await Helpers.HttpHelper.GetAsync($"{Api}/newedenapi/status/game/month?ServerType={serverType}&&Date={date}");
            return JsonConvert.DeserializeObject<IEnumerable<ServerStatusPerDay>>(result);
        }

        private static async Task<IEnumerable<ServerStatusPerHour>> GetServerStatusPerHourAsync(DateTime date, int serverType)
        {
            string result = await Helpers.HttpHelper.GetAsync($"{Api}/newedenapi/status/game/day?ServerType={serverType}&&Date={date}");
            return JsonConvert.DeserializeObject<IEnumerable<ServerStatusPerHour>>(result);
        }
    }
}
