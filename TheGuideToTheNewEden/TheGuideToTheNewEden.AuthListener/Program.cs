using System.Text;

try
{
    StringBuilder stringBuilder = new StringBuilder();
    foreach (var arg in args)
    {
        Console.WriteLine($"收到信息：{arg}");
        stringBuilder.Append(arg.ToString());
    }
    string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Auth");
    if (!Directory.Exists(folder))
    {
        Console.WriteLine($"新建文件夹：{folder}");
        Directory.CreateDirectory(folder);
    }
    string filepath = Path.Combine(folder, "msg.txt");
    Console.WriteLine($"开始写入信息到文件{filepath}");
    File.WriteAllText(filepath, stringBuilder.ToString());
    Console.WriteLine("完成，请关闭此窗口");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
while(true)
{

}