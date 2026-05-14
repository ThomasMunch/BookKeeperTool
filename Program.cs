using BookKeeperTool.Models;
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
    extension = "FD*.txt";
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

// For Apple: load all financial_report*.csv files and build dated currency rates
Dictionary<DateOnly, List<CurrencyRate>> ttrRatesByDate = new();
Dictionary<DateOnly, bool> ttrIsPaidByDate = new();
if (choice.Equals("A", StringComparison.OrdinalIgnoreCase))
{
    var finFiles = Directory.GetFiles(folderPath, "financial_report*.csv");
    foreach (var finFile in finFiles)
    {
        try
        {
            var (date, rates, isPaid) = AppleFinancialReportParser.Parse(finFile);
            ttrRatesByDate[date] = rates;
            ttrIsPaidByDate[date] = isPaid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advarsel: Kunne ikke parse {Path.GetFileName(finFile)}: {ex.Message}");
        }
    }

    if (ttrRatesByDate.Count > 0)
        Console.WriteLine($"Indlæste {ttrRatesByDate.Count} Apple financial report(er) med valutakurser ({string.Join(", ", ttrRatesByDate.Keys.OrderBy(d => d).Select(d => d.ToString("yyyy-MM")))}).");
    else
        Console.WriteLine("Ingen financial_report*.csv fundet – bruger standardvalutakurser.");
}


foreach (var file in files)
{
    try
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        var yearMonth = parser.GetYearMonthFromFileName(fileName);
        var payoutDate = parser.GetPayoutDateFromFileName(fileName);

        BookKeeperTool.Models.RevenueResult result;

        if (parser is AppleParser appleParser && ttrRatesByDate.Count > 0)
        {
            var fdDate = new DateOnly(
                int.Parse(yearMonth.Split('-')[0]),
                int.Parse(yearMonth.Split('-')[1]), 1);

            var bestTtrDate = ttrRatesByDate.Keys
                .Where(d => d <= fdDate)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            var rates = bestTtrDate != default ? ttrRatesByDate[bestTtrDate] : null;
            // isPaid: only true if there is a financial report for exactly this FD month and it is marked paid
            bool isPaid = ttrIsPaidByDate.TryGetValue(fdDate, out var p) && p;
            result = appleParser.Parse(file, rates, isPaid);
        }
        else
        {
            result = parser.Parse(file);
        }

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

foreach (var (yearMonth, payoutDate, result) in results)
{
    Console.WriteLine($"--- {yearMonth} ---");

    if (result.Source == "Apple")
    {
        if (!string.IsNullOrEmpty(result.SettlementStatus))
            Console.WriteLine($"Status: {result.SettlementStatus}");

        Console.WriteLine($"Exchange rates: {result.ExchangeRateSourceDescription}");
        Console.WriteLine();

        foreach (var w in result.Warnings)
            Console.WriteLine($"⚠ {w}");
        if (result.Warnings.Count > 0) Console.WriteLine();

        const int lw = 52; // label width
        const int nw = 12; // number width
        string A(decimal v) => v.ToString("N2").PadLeft(nw);
        string P(decimal v) => (v.ToString("F1") + "%").PadLeft(nw);

        Console.WriteLine($"{"Kundebetaling brutto inkl. Apple-opkrævede skatter:".PadRight(lw)}{A(result.GrossCustomerPayments)}");
        Console.WriteLine($"{"Apple-opkrævet kundemoms/skatter:".PadRight(lw)}{A(-result.CustomerTax)}");
        Console.WriteLine($"{"Omsætning ekskl. kundemoms/skatter:".PadRight(lw)}{A(result.Revenue)}");
        Console.WriteLine($"{"Apple commission (Konto: 4075):".PadRight(lw)}{A(-result.AppleCommission)}");

        var feePercent = result.Revenue != 0
            ? result.AppleCommission / result.Revenue * 100
            : 0;
        Console.WriteLine($"{"Apple commission %:".PadRight(lw)}{P(feePercent)}");
        Console.WriteLine($"{"Netto til udbetaling, estimeret DKK (Konto 53900):".PadRight(lw)}{A(result.NetPayout)}");
        Console.WriteLine($"{"Reverse charge grundlag:".PadRight(lw)}{A(result.ReverseChargeBase)}");
        Console.WriteLine($"{"Reverse charge moms:".PadRight(lw)}{A(result.ReverseChargeVAT)}");
        var control = Math.Round(result.NetPayout + result.AppleCommission, 2);
        var revRounded = Math.Round(result.Revenue, 2);
        if (Math.Abs(control - revRounded) > 0.05m)
            Console.WriteLine($"⚠ Kontrolfejl: NetPayout + Commission = {control:N2}, Revenue = {revRounded:N2}");

        Console.WriteLine($"Forventet udbetalingstidspunkt: {payoutDate}");
    }
    else
    {
        Console.WriteLine($"Omsætning: {result.Revenue:N2}");
        Console.WriteLine($"{vendorName} fee: {result.GoogleOrAppleFee:N2}");

        var feePercent = result.Revenue != 0
            ? Math.Abs(result.GoogleOrAppleFee) / result.Revenue * 100
            : 0;
        Console.WriteLine($"Fee %: {feePercent:F1}%");
        Console.WriteLine($"Netto til udbetaling: {result.NetPayout:N2}");
        Console.WriteLine($"Reverse charge grundlag: {result.ReverseChargeBase:N2}");
        Console.WriteLine($"Reverse charge moms: {result.ReverseChargeVAT:N2}");
        Console.WriteLine($"Forventet udbetalingstidspunkt: {payoutDate}");
    }

    Console.WriteLine();
}

if (vendorName == "Apple")
{
    Console.WriteLine("Bogføring i Dinero (pr. måned):");
    Console.WriteLine("Linje 1: 53900 Tilgodehavende Apple / 1070 Apple App Store omsætning = NetPayout");
    Console.WriteLine("Linje 2: 4075 Apple App Store fee / 1070 Apple App Store omsætning = AppleCommission");
    Console.WriteLine("Apple-opkrævet kundemoms/skatter bogføres ikke.");
    Console.WriteLine();
}

Console.WriteLine("Tryk på en tast for at lukke...");
Console.ReadKey();