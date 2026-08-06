using NDesk.Options;

namespace BDU.Tools.CodeFormat;

public static class Program
{
    public static int Main(string[] args)
    {
        bool help = false;
        List<string> ignoredPaths = new List<string>();
        List<string> pathsToCheck = new List<string>();
        List<string> current = new List<string>();

        OptionSet options = new OptionSet()
        {
            { "i=|ignore=", "Specifies the folder(s) to ignore when checking the formats.", v => { ignoredPaths.Add(v); current = ignoredPaths; } },
            { "h|help|?", "Shows this message.", v => help = v != null },
            { "p=|path=", "Specifies the path(s) to format check.", v => { ignoredPaths.Add(v); current = ignoredPaths; } },
            { "<>", v => current.Add(v) }
        };
        options.Parse(args);

        Console.WriteLine("Ignored:");

        foreach (string ignored in ignoredPaths)
        {
            Console.WriteLine(ignored);
        }

        Console.WriteLine("\nIncluded:");
        
        foreach (string included in pathsToCheck)
        {
            Console.WriteLine(included);
        }

        if (help)
        {
            Console.WriteLine("Usage: CodeFormat [OPTIONS]+ path(s) to check");
            Console.WriteLine("Checks the formatting of a specified folder (and subdirectories) alongside options.\n");
            Console.WriteLine("Options:");
            options.WriteOptionDescriptions(Console.Out);
            return 0;
        }


        
        return 0;
    }
}