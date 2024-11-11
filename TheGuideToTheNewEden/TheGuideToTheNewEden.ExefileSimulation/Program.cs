Console.WriteLine("Hello, NewEden!");
Console.Write("Input character name or full title name:");
string input = Console.ReadLine();
if(input.Contains("EVE - "))
{
    Console.Title = input;
}
else
{
    Console.Title = $"EVE - {input}";
}
while (true)
{
    Console.ReadLine();
}