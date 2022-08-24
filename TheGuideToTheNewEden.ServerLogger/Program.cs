// See https://aka.ms/new-console-template for more information
Console.WriteLine("EVE服务器状态记录程序");
Console.Write("输入间隔时间(s)：");
TheGuideToTheNewEden.ServerLogger.LogService.Duration = int.Parse(Console.ReadLine());
TheGuideToTheNewEden.ServerLogger.LogService.Begin();
while (Console.ReadLine() == "q")
    break;
