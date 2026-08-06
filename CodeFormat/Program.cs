using NDesk.Options;

namespace BDU.Tools.CodeFormat;

public static class Program
{
    public static int Main(string[] args)
    {
        bool help = false;
        List<string> ignoredPaths = new List<string>();

        OptionSet options = new OptionSet()
        {
            { "i|ignore=", "Specifies a folder name to ignore when checking the formats.", ignoredPaths.Add },
            { "h|help|?", "Shows this message.", v => help = v != null }
        };

        if (help)
        {
            Console.WriteLine("Usage: CodeFormat [OPTIONS]+ path(s) to check");
            Console.WriteLine("Checks the formatting of a specified folder (and subdirectories) alongside options.\n");
            Console.WriteLine("Options:");
            options.WriteOptionDescriptions(Console.Out);
            return 0;
        }

        List<string> extra = options.Parse(args);
        string path = string.Empty;

        if (extra.Count > 0)
        {
            path = string.Join(" ", extra.ToArray());
        }
        else
        {
            throw new ArgumentException("No path(s) specified.");
        }
        
        
        
        return 0;
    }
}