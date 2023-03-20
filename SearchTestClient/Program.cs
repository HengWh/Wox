// See https://aka.ms/new-console-template for more information
using SearchTestClient;
using Wox.Proto;

Console.WriteLine("Hello, World!");

var path1 = @"C:\$Recycle.Bin\S-1-5-18\$RZJHIGY\测试-123";
var path2 = @"D:\NutDemo\NutTest\新建文件夹\测试-123";
var path3 = @"测";
var path4 = @"D";
var path5 = @"$";

var byte1= FuzzyUtil.PackValue(path1, false);
var byte2= FuzzyUtil.PackValue(path2, false);
var byte3= FuzzyUtil.PackValue(path3, false);
var byte4= FuzzyUtil.PackValue(path4, false);
var byte5= FuzzyUtil.PackValue(path5, false);
Console.WriteLine($"byte1 length is {byte1.Length}, byte2 length is {byte2.Length}");


return;

while (true)
{
    Console.WriteLine("\n############### Please Input command \"update\" or \"search\" #################");
    var str = Console.ReadLine();
    str = str?.Trim().ToLower();
    if (string.IsNullOrEmpty(str))
        continue;

    if (str.StartsWith("update"))
    {
        var path = str.Substring(6).TrimStart();
        FzfTest.UpdatePath(path);
    }
    else if (str.StartsWith("search"))
    {
        var path = str.Substring(6).TrimStart();
        FzfTest.Search(path.Split(' '));
    }
    else if (str.Equals("echo"))
    {
        FzfTest.Echo();
    }
    else if (str.Equals("echostream"))
    {
        FzfTest.EchoStream();
    }
    else
    {
        Console.WriteLine("Invalid input!\n");
    }
}
