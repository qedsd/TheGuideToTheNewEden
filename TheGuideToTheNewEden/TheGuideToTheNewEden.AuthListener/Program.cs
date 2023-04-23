using System.Text;

StringBuilder stringBuilder = new StringBuilder();
foreach(var arg in args)
{
    Console.WriteLine(arg);
    stringBuilder.Append(arg.ToString());
}
string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Auth");
if(!Directory.Exists(folder))
{
    Directory.CreateDirectory(folder);
}
string filepath = Path.Combine(folder, "msg.txt");
File.WriteAllText(filepath,stringBuilder.ToString());
Console.ReadLine();