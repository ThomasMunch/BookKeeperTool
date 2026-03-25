using BookKeeperTool.Parsers;

string folderPath;

// Input (argument eller prompt)
if (args.Length > 0)
{
    folderPath = args[0];
}
else
{
    Console.Write("Indtast mappe med CSV filer: ");
    folderPath = Console.ReadLine()?.Trim('"') ?? "";
}

if (string.IsNullOrWhiteSpace(folderPath))
{
    Console.WriteLine("Ingen mappe angivet.");
    return;
}

if (!Directory.Exists(folderPath))
{
    Console.WriteLine("Mappe ikke fundet!");
    return;
}

// Find alle CSV filer
var files = Directory.GetFiles(folderPath, "*.csv");

if (files.Length == 0)
{
    Console.WriteLine("Ingen CSV filer fundet.");
    return;
}

var parser = new GoogleParser();

Console.WriteLine("\n===== RESULTATER =====\n");

foreach (var file in files)
{
    try
    {
        var result = parser.Parse(file);

        // Udtræk måned fra filnavn (fx 2025-07_PlayApps.csv)
        var fileName = Path.GetFileNameWithoutExtension(file);
        var month = fileName.Split('_')[0];

        var payoutMonth = GetNextMonth(month);

        Console.WriteLine($"--- {month} ---");
        Console.WriteLine($"Omsætning: {result.Revenue:N2}");
        Console.WriteLine($"Google fee: {result.GoogleFee:N2}");
        Console.WriteLine($"Netto: {result.NetPayout:N2}");
        Console.WriteLine($"Reverse charge grundlag: {result.ReverseChargeBase:N2}");
        //Console.WriteLine($"Reverse charge netto: {result.ReverseChargeNet:N2}");
        Console.WriteLine($"Reverse charge moms: {result.ReverseChargeVAT:N2}");

        Console.WriteLine($"Forventet udbetaling: medio {payoutMonth}");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Fejl i fil: {file}");
        Console.WriteLine(ex.Message);
        Console.WriteLine();
    }
}

string GetNextMonth(string month)
{
    var date = DateTime.ParseExact(month + "-01", "yyyy-MM-dd", null);
    var next = date.AddMonths(1);
    return next.ToString("yyyy-MM");
}