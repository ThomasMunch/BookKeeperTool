using BookKeeperTool.Parsers;

string folderPath = "C:\\Reports";

// Input (argument eller prompt)
//if (args.Length > 0)
//{
//    folderPath = args[0];
//}
//else
//{
//    Console.Write("Indtast mappe med Financial Reports filer: ");
//    folderPath = Console.ReadLine()?.Trim('"') ?? "";
//}

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

Console.Write("Vælg (G)oogle eller (A)pple: ");
var choice = Console.ReadLine()?.Trim('"') ?? "";

IParser parser;
string extension;
string vendorName = "";

if (choice.Equals("G", StringComparison.OrdinalIgnoreCase))
{
    parser = new GoogleParser();
    extension = "PlayApps*.csv";
    vendorName = "Google"; 
}

else if (choice.Equals("A", StringComparison.OrdinalIgnoreCase))
{
    parser = new AppleParser();
    extension = "*.txt";
    vendorName = "Apple"; 
}

else
{
    Console.WriteLine("Ugyldigt valg.");
    return;
}

// Find alle CSV filer(Google) eller txt filer(Apple) i mappen
var files = Directory.GetFiles(folderPath, extension);

if (files.Length == 0)
{
    Console.WriteLine($"Ingen filer fundet med {extension}");
    return;
}

// Parse alle filer til en liste og sorter på payoutDate
var results = new List<(string Month, DateOnly PayoutDate, BookKeeperTool.Models.RevenueResult Result)>();

foreach (var file in files)
{
    try
    {
        var result = parser.Parse(file);
        var fileName = Path.GetFileNameWithoutExtension(file);
        var yearMonth = parser.GetYearMonthFromFileName(fileName);
        var payoutDate = parser.GetPayoutDateFromFileName(fileName);

        results.Add((yearMonth, payoutDate, result));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Fejl i fil: {file}");
        Console.WriteLine(ex.Message);
        Console.WriteLine();
    }
}

results.Sort((a, b) => a.PayoutDate.CompareTo(b.PayoutDate));

Console.WriteLine($"\n===== {vendorName} RESULTATER =====\n");

//foreach (var (yearMonth, payoutDate, result) in results)
//{
//    Console.WriteLine($"--- {yearMonth} ---");
//    Console.WriteLine($"Omsætning: {result.Revenue:N2}");
//    Console.WriteLine($"{vendorName} fee: {result.GoogleOrAppleFee:N2}");

//    var feePercent = result.Revenue != 0
//        ? Math.Abs(result.GoogleOrAppleFee) / result.Revenue * 100
//        : 0;

//    Console.WriteLine($"Fee %: {feePercent:F1}%");
//    Console.WriteLine($"Netto til udbetaling: {result.NetPayout:N2}");
//    Console.WriteLine($"Reverse charge grundlag: {result.ReverseChargeBase:N2}");
//    Console.WriteLine($"Reverse charge moms: {result.ReverseChargeVAT:N2}");
//    Console.WriteLine($"Forventet udbetalingstidspunkt: {payoutDate}");
//    Console.WriteLine();
//}
foreach (var (yearMonth, payoutDate, result) in results)
{
    Console.WriteLine($"--- {yearMonth} ---");

    if (result.Source == "Apple")
    {
        Console.WriteLine($"Kundebetaling brutto inkl. Apple-opkrævede skatter: {result.GrossCustomerPayments:N2}");
        Console.WriteLine($"Apple-opkrævet kundemoms/skatter: {-result.CustomerTax:N2}");
        Console.WriteLine($"Omsætning ekskl. kundemoms/skatter: {result.Revenue:N2}");
        Console.WriteLine($"Apple commission: {result.GoogleOrAppleFee:N2}");

        var appleCommissionPercent = result.Revenue != 0
            ? Math.Abs(result.GoogleOrAppleFee) / result.Revenue * 100
            : 0;

        Console.WriteLine($"Apple commission %: {appleCommissionPercent:F1}%");

        var totalWithheld = result.CustomerTax + result.AppleCommission;
        Console.WriteLine($"Tilbageholdt i alt, skat + commission: {-totalWithheld:N2}");
    }
    else
    {
        Console.WriteLine($"Omsætning: {result.Revenue:N2}");
        Console.WriteLine($"{vendorName} fee: {result.GoogleOrAppleFee:N2}");

        var feePercent = result.Revenue != 0
            ? Math.Abs(result.GoogleOrAppleFee) / result.Revenue * 100
            : 0;

        Console.WriteLine($"Fee %: {feePercent:F1}%");
    }

    Console.WriteLine($"Netto til udbetaling: {result.NetPayout:N2}");
    Console.WriteLine($"Reverse charge grundlag: {result.ReverseChargeBase:N2}");
    Console.WriteLine($"Reverse charge moms: {result.ReverseChargeVAT:N2}");
    Console.WriteLine($"Forventet udbetalingstidspunkt: {payoutDate}");
    Console.WriteLine();
}
Console.WriteLine("Tryk på en tast for at lukke...");
Console.ReadKey();