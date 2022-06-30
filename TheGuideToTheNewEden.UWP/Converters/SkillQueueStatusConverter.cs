using System;
using System.Text;
using TheGuideToTheNewEden.UWP.Helpers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace TheGuideToTheNewEden.UWP.Converters
{
    public sealed class SkillQueueStatusConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var skillQueue = value as Core.Models.Character.SkillQueue;
            if (skillQueue == null)
            {
                return null;
            }
            else
            {
                if(skillQueue.IsDone)
                {
                    return "SkillQueue_Done".GetLocalized();
                }
                else if(skillQueue.IsPause)
                {
                    return "SkillQueue_Pause".GetLocalized();
                }
                else
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    if(skillQueue.Duration.Days != 0)
                    {
                        stringBuilder.Append($"{skillQueue.Duration.Days}d ");
                    }
                    if(skillQueue.Duration.Days != 0 && skillQueue.Duration.Hours != 0)
                    {
                        stringBuilder.Append($"{skillQueue.Duration.Hours}h ");
                    }
                    stringBuilder.Append($"{skillQueue.Duration.Minutes}m");
                    return stringBuilder.ToString();
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
