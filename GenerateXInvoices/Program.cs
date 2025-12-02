// <PackageReference Include="Bogus" Version="35.5.0" />
// <PackageReference Include="Spectre.Console" Version="0.49.1" />

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Bogus;
using Spectre.Console;
using ValidationResult = Spectre.Console.ValidationResult;

namespace GenerateXInvoices;

// ========================= Models =========================
internal sealed record InvoiceScan(
    string Path,
    string InvoiceNumber,
    DateTime IssueDate,
    DateTime? DueDate,
    string Customer,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string Currency,
    int ItemCount,
    IReadOnlyList<decimal> VatRates,
    bool HasJsonSidecar,
    long FileBytes,
    DateTime LastWriteUtc
);

public sealed class Invoice
{
    public string InvoiceNumber { get; set; } = "";
    public DateTime IssueDate
    {
        get; set;
    }
    public DateTime? DueDate
    {
        get; set;
    }
    public Customer BillTo { get; set; } = new();
    public string Currency { get; set; } = "EUR";
    public List<LineItem> Items { get; set; } = [];
    public decimal Subtotal
    {
        get; set;
    }
    public decimal TaxRate
    {
        get; set;
    }         // default VAT
    public decimal TaxAmount
    {
        get; set;
    }
    public decimal Total
    {
        get; set;
    }
    public string PaymentTerms { get; set; } = "Net 14";
    public string Iban { get; set; } = "";
    public string Bic { get; set; } = "";
    public string VendorVatId { get; set; } = "";
    public string CustomerVatId { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class Customer
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public Address Address { get; set; } = new();
}

public sealed class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public string Country { get; set; } = "";
}

public sealed class LineItem
{
    public string Sku { get; set; } = "";
    public string Description { get; set; } = "";
    public int Qty
    {
        get; set;
    }
    public decimal UnitPrice
    {
        get; set;
    }
    public decimal LineTotal
    {
        get; set;
    }
    public decimal VatRate
    {
        get; set;
    } // optional per-line VAT
}

// ========================= Settings =========================
// 1) NEW: Simple localization helper (drop anywhere in the file, e.g. above Program)
public static class L
{
    private static readonly Dictionary<string, Dictionary<string, string>> _dict = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(StringComparer.OrdinalIgnoreCase),
        ["de"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Select an option:"] = "Wähle eine Option:",
            ["Generate multiple invoices"] = "Mehrere Rechnungen erzeugen",
            ["Generate one invoice (use DateTime.Now)"] = "Eine Rechnung erzeugen (mit DateTime.Now)",
            ["Export CSV summary"] = "CSV-Zusammenfassung exportieren",
            ["Delete All Invoices"] = "Alle Rechnungen löschen",
            ["Show folder statistics"] = "Ordnerstatistiken anzeigen",
            ["Find invoices"] = "Rechnungen suchen",
            ["Validate invoices (integrity)"] = "Rechnungen validieren (Integrität)",
            ["Aging report (overdue buckets)"] = "Fälligkeitsreport (Überfälligkeits-Buckets)",
            ["Top customers by revenue"] = "Top-Kunden nach Umsatz",
            ["Open invoices folder"] = "Rechnungsordner öffnen",
            ["Exit"] = "Beenden",
            ["Exiting..."] = "Beenden...",
            ["Folder:"] = "Ordner:",
            ["How many invoices to generate?"] = "Wie viele Rechnungen erzeugen?",
            ["Must be > 0."] = "Muss > 0 sein.",
            ["Fixed line items per invoice?"] = "Feste Positionen pro Rechnung?",
            ["(Enter for {0}–{1})"] = "(Enter für {0}-{1})",
            ["Enter 1..100000 or leave empty."] = "Gib 1..100000 ein oder lass leer.",
            ["Also save JSON sidecar?"] = "JSON-Sidecar ebenfalls speichern?",
            ["Generating invoices"] = "Erzeuge Rechnungen",
            ["All invoices generated."] = "Alle Rechnungen erzeugt.",
            ["Generating one invoice..."] = "Erzeuge eine Rechnung...",
            ["Invoice created."] = "Rechnung erstellt.",
            ["CSV exported:"] = "CSV exportiert:",
            ["No invoices found to delete."] = "Keine Rechnungen zum Löschen gefunden.",
            ["Are you sure you want to delete all invoices?"] = "Sollen wirklich alle Rechnungen gelöscht werden?",
            ["Deletion process canceled."] = "Löschvorgang abgebrochen.",
            ["Delete invoices"] = "Lösche Rechnungen",
            ["Error deleting {0}: {1}"] = "Fehler beim Löschen von {0}: {1}",
            ["All invoices have been deleted."] = "Alle Rechnungen wurden gelöscht.",
            ["Press any key to continue..."] = "Taste drücken um fortzufahren...",
            ["Settings"] = "Einstellungen",
            ["Locale (current: {0})"] = "Sprache (aktuell: {0})",
            ["Currency (current: {0})"] = "Währung (aktuell: {0})",
            ["Default VAT (current: {0})"] = "Standard-MwSt (aktuell: {0})",
            ["Fixed items (current: {0})"] = "Feste Positionen (aktuell: {0})",
            ["Seed (current: {0})"] = "Seed (aktuell: {0})",
            ["JSON sidecar (current: {0})"] = "JSON-Sidecar (aktuell: {0})",
            ["Back"] = "Zurück",
            ["Enter locale (e.g., en, de, fr, en_GB):"] = "Sprache eingeben (z. B. en, de, fr, en_GB):",
            ["Enter currency code (ISO 4217, e.g., EUR, USD):"] = "Währungscode eingeben (ISO 4217, z. B. EUR, USD):",
            ["Enter VAT as fraction (e.g., 0.19):"] = "MwSt als Bruch eingeben (z. B. 0,19):",
            ["Enter fixed items 1..500 or empty for auto:"] = "Feste Positionen 1..500 oder leer für automatisch:",
            ["Invalid number."] = "Ungültige Zahl.",
            ["Enter RNG seed:"] = "RNG-Seed eingeben:",
            ["Enable JSON sidecar on save?"] = "JSON-Sidecar beim Speichern aktivieren?",
            ["Failed to open folder: {0}"] = "Ordner konnte nicht geöffnet werden: {0}",

            // Stats (Titel/Spalten)
            ["Folder Overview"] = "Ordnerübersicht",
            ["Metric"] = "Metrik",
            ["Value"] = "Wert",
            ["Invoice files (XML)"] = "Rechnungsdateien (XML)",
            ["Total size (MB)"] = "Gesamtgröße (MB)",
            ["Oldest file"] = "Älteste Datei",
            ["Newest file"] = "Neueste Datei",
            ["Median file size (KB)"] = "Median Dateigröße (KB)",
            ["JSON sidecars"] = "JSON-Sidecars",
            ["Totals by Currency"] = "Summen nach Währung",
            ["Currency"] = "Währung",
            ["Invoices"] = "Rechnungen",
            ["Sum Total"] = "Summe Gesamt",
            ["VAT Rates (presence across line items)"] = "MwSt-Sätze (Vorkommen in Positionen)",
            ["VAT Rate"] = "MwSt-Satz",
            ["Occurrences (items)"] = "Vorkommen (Positionen)",
            ["Invoices touched"] = "Betroffene Rechnungen",
            ["Invoices per day (last 30 days)"] = "Rechnungen pro Tag (letzte 30 Tage)",
            ["Daily totals (last 30 days)"] = "Tagessummen (letzte 30 Tage)",
            ["Day"] = "Tag",
            ["#"] = "#",
            ["Sum"] = "Summe",
            ["Top 10 Customers by Revenue"] = "Top 10 Kunden nach Umsatz",
            ["Customer"] = "Kunde",
            ["Revenue"] = "Umsatz",
            ["Aging Buckets (by DueDate)"] = "Fälligkeits-Buckets (nach Fälligkeitsdatum)",
            ["Bucket"] = "Bucket",
            ["Count"] = "Anzahl",
            ["Top 5 Invoices by Total"] = "Top 5 Rechnungen nach Gesamt",
            ["Invoice"] = "Rechnung",
            ["IssueDate"] = "Rechnungsdatum",
            ["DueDate"] = "Fällig am",
            ["Quick integrity summary"] = "Schnelle Integritätsübersicht",
            ["Duplicates:"] = "Duplikate:",
            ["Anomalies (sums/tax mismatch):"] = "Anomalien (Summen/MwSt abweichend):",
            ["Missing DueDate:"] = "Fehlendes Fälligkeitsdatum:",

            // Search/Validate/Aging/Top
            ["Search (customer/invoice no., case-insensitive):"] = "Suche (Kunde/Rechnungsnr., ohne Groß/Kleinschreibung):",
            ["No matches."] = "Keine Treffer.",
            ["Results ({0})"] = "Ergebnisse ({0})",
            ["Issue"] = "Rechnungsdatum",
            ["Due"] = "Fällig",
            ["Path"] = "Pfad",
            ["Validation Report"] = "Validierungsbericht",
            ["Anomalies (first 20 of {0}):"] = "Anomalien (erste 20 von {0}):",
            ["Validation CSV:"] = "Validierungs-CSV:",
            ["Aging Details"] = "Fälligkeitsdetails",
            ["Days overdue"] = "Tage überfällig",
            ["Last invoice"] = "Letzte Rechnung",
        }
    };

    private static string _lang = "en";
    public static void Use(string locale)
    {
        _lang = string.IsNullOrWhiteSpace(locale) ? "en" : (locale.Split('-', '_')[0].ToLowerInvariant()) switch
        {
            "de" => "de",
            _ => "en"
        };
        var culture = new CultureInfo(locale);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static string T(string key, params object[] args)
    {
        if (_dict.TryGetValue(_lang, out var map) && map.TryGetValue(key, out var v))
        {
            return args is { Length: > 0 } ? string.Format(v, args) : v;
        }

        return args is { Length: > 0 } ? string.Format(key, args) : key; // fallback to key
    }
}


public sealed class GeneratorSettings
{
    public string Locale { get; set; } = "en";
    public string Currency { get; set; } = "EUR";
    public decimal DefaultVatRate { get; set; } = 0.19m;
    public int? FixedItemCount { get; set; } = null;
    public int Seed { get; set; } = 42;
    public bool SaveJsonSidecar { get; set; } = false;
    public bool PrettyPrintXml { get; set; } = false;
    public string InvoicePrefix { get; set; } = "INV";
    public int MinLineItems { get; set; } = 100;
    public int MaxLineItems { get; set; } = 500;
    public decimal MinUnitPrice { get; set; } = 2m;
    public decimal MaxUnitPrice { get; set; } = 1200m;
    public int[] PaymentTermsDays { get; set; } = [7, 14, 30];
    public bool AutoIncrementInvoiceNumber { get; set; } = true;
    public string DateFormat { get; set; } = "yyyy-MM-dd";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GenerateXInvoices",
        "settings.json"
    );

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static GeneratorSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<GeneratorSettings>(json, SerializerOptions);
                return settings ?? new GeneratorSettings();
            }
        }
        catch (Exception ex)
        {
            // Fallback bei Fehler
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not load settings ({ex.Message}). Using defaults.[/]");
        }

        return new GeneratorSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, SerializerOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not save settings ({ex.Message}).[/]");
        }
    }

    public static string GetSettingsPath() => SettingsPath;
}

// ==================== Bogus Factory ====================
public static class InvoiceFakerFactory
{
    private static readonly decimal[] VatPool = [0.19m, 0.07m, 0m];
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static Faker<Invoice> Create(GeneratorSettings s)
    {
        // KEIN Randomizer.Seed global setzen

        var addrFaker = new Faker<Address>(s.Locale)
            .RuleFor(a => a.Street, f => f.Address.StreetAddress())
            .RuleFor(a => a.ZipCode, f => f.Address.ZipCode())
            .RuleFor(a => a.City, f => f.Address.City())
            .RuleFor(a => a.Country, f => f.Address.Country());

        var customerFaker = new Faker<Customer>(s.Locale)
            .RuleFor(c => c.Name, f => f.Company.CompanyName())
            .RuleFor(c => c.Email, f => f.Internet.Email())
            .RuleFor(c => c.Address, f => addrFaker.Generate());

        var lineItemFaker = new Faker<LineItem>(s.Locale)
            .RuleFor(li => li.Sku, f => f.Commerce.Ean13())
            .RuleFor(li => li.Description, f => f.Commerce.ProductName())
            .RuleFor(li => li.Qty, f => f.Random.Int(1, 12))
            .RuleFor(li => li.UnitPrice, f => f.Random.Decimal(s.MinUnitPrice, s.MaxUnitPrice))
            .RuleFor(li => li.VatRate, f => f.PickRandom(VatPool))
            .FinishWith((_, li) => li.LineTotal = RoundMoney(li.UnitPrice * li.Qty));

        return new Faker<Invoice>(s.Locale)
            // InvoiceNumber NICHT hier setzen; das macht Parallelität einfacher
            .RuleFor(i => i.IssueDate, f => f.Date.Between(DateTime.Today.AddDays(-30), DateTime.Today))
            .RuleFor(i => i.DueDate, (f, i) => i.IssueDate.AddDays(f.PickRandom(s.PaymentTermsDays)))
            .RuleFor(i => i.BillTo, _ => customerFaker.Generate())
            .RuleFor(i => i.Currency, _ => s.Currency)
            .RuleFor(i => i.PaymentTerms, (f, i) => $"Net {(i.DueDate!.Value - i.IssueDate).Days}")
            .RuleFor(i => i.Items, f =>
            {
                var count = s.FixedItemCount ?? f.Random.Int(s.MinLineItems, s.MaxLineItems);
                return lineItemFaker.Generate(count);
            })
            .RuleFor(i => i.TaxRate, _ => s.DefaultVatRate)
            .RuleFor(i => i.Iban, _ => FakeIbanLike("DE"))
            .RuleFor(i => i.Bic, f => f.Finance.Bic())
            .RuleFor(i => i.VendorVatId, f => $"DE{f.Random.Int(100000000, 999999999)}")
            .RuleFor(i => i.CustomerVatId, f => $"DE{f.Random.Int(100000000, 999999999)}")
            .RuleFor(i => i.Notes, f => f.Lorem.Sentence())
            .FinishWith((_, i) =>
            {
                i.Subtotal = RoundMoney(i.Items.Sum(x => x.LineTotal));
                i.TaxAmount = i.Items.Sum(x => RoundMoney(x.LineTotal * x.VatRate));
                i.Total = RoundMoney(i.Subtotal + i.TaxAmount);
            });
    }

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private static string FakeIbanLike(string country)
    {
        var f = new Faker();
        return country + f.Random.Number(10, 99)
             + " " + f.Random.ReplaceNumbers("#### #### #### #### #### ##");
    }
}

// ==================== XML / JSON ====================
public static class InvoiceXml
{
    public static XDocument ToXml(Invoice inv)
    {
        var root = new XElement("Invoice",
            new XElement("InvoiceNumber", inv.InvoiceNumber),
            new XElement("IssueDate", inv.IssueDate.ToString("yyyy-MM-dd")),

            // NEU: nur wenn gesetzt
            inv.DueDate.HasValue
                ? new XElement("DueDate", inv.DueDate?.ToString("yyyy-MM-dd") ?? "")
                : null,

            new XElement("Currency", inv.Currency),
            new XElement("PaymentTerms", inv.PaymentTerms),
            new XElement("Customer",
                new XElement("Name", inv.BillTo.Name),
                new XElement("Email", inv.BillTo.Email),
                new XElement("Address",
                    new XElement("Street", inv.BillTo.Address.Street),
                    new XElement("ZipCode", inv.BillTo.Address.ZipCode),
                    new XElement("City", inv.BillTo.Address.City),
                    new XElement("Country", inv.BillTo.Address.Country)
                )
            ),
            new XElement("Financials",
                new XElement("Subtotal", inv.Subtotal),
                new XElement("StandardTaxRate", inv.TaxRate),
                new XElement("TaxAmount", inv.TaxAmount),
                new XElement("Total", inv.Total),
                new XElement("IBAN", inv.Iban),
                new XElement("BIC", inv.Bic),
                new XElement("VendorVAT", inv.VendorVatId),
                new XElement("CustomerVAT", inv.CustomerVatId)
            ),
            new XElement("Items",
                inv.Items.Select(li =>
                    new XElement("Item",
                        new XElement("SKU", li.Sku),
                        new XElement("Description", li.Description),
                        new XElement("Quantity", li.Qty),
                        new XElement("UnitPrice", li.UnitPrice),
                        new XElement("VATRate", li.VatRate),
                        new XElement("LineTotal", li.LineTotal)
                    )
                )
            ),
            new XElement("Notes", inv.Notes)
        );

        return new XDocument(root);
    }
}

public static class InvoiceJson
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static string ToJson(Invoice inv) => JsonSerializer.Serialize(inv, Opts);
}

// ==================== CSV Export ====================
public static class CsvExporter
{
    public static void ExportSummary(string folder, string csvPath)
    {
        // Optimiert: Parallelisierter CSV-Export mit XDocument
        var files = Directory.EnumerateFiles(folder, "Invoice_*.xml").ToArray();
        var lines = new ConcurrentBag<string>();

        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, f =>
        {
            try
            {
                var doc = XDocument.Load(f, LoadOptions.None);
                var root = doc.Root;
                if (root == null)
                {
                    return;
                }

                var invNo = root.Element("InvoiceNumber")?.Value ?? "";
                var issue = root.Element("IssueDate")?.Value ?? "";
                var due = root.Element("DueDate")?.Value ?? "";
                var cust = root.Element("Customer")?.Element("Name")?.Value ?? "";
                var currency = root.Element("Currency")?.Value ?? "";
                var financials = root.Element("Financials");
                var subtotal = financials?.Element("Subtotal")?.Value ?? "0";
                var tax = financials?.Element("TaxAmount")?.Value ?? "0";
                var total = financials?.Element("Total")?.Value ?? "0";

                var line = $"{invNo},{issue},{due},{Escape(cust)},{subtotal},{tax},{total},{currency}";
                lines.Add(line);
            }
            catch { /* ignore */ }
        });

        using var fs = new FileStream(csvPath, new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan,
            BufferSize = 256 * 1024
        });
        using var sw = new StreamWriter(fs, new UTF8Encoding(false), 128 * 1024);

        sw.WriteLine("InvoiceNumber,IssueDate,DueDate,Customer,Subtotal,TaxAmount,Total,Currency");
        foreach (var line in lines.OrderBy(l => l))
        {
            sw.WriteLine(line);
        }
    }

    private static string Escape(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        return s;
    }
}

// ==================== App ====================
internal partial class Program
{
    private static int _invoiceCounter = 100000;

    // Optimiert: Semaphore basierend auf verfügbaren Threads
    private static readonly SemaphoreSlim _ioGate = new(Math.Max(4, Environment.ProcessorCount));

    // Pool für wiederverwendbare Faker-Instanzen mit Größenlimit
    private static readonly ConcurrentBag<Faker<Invoice>> _fakerPool = [];
    private const int MaxFakerPoolSize = 32; // Verhindert unbegrenztes Wachstum

    // Application metadata - Version automatisch aus Assembly
    private static readonly string AppVersion = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(Program).Assembly.GetName().Version?.ToString(3)
        ?? "1.0.0";

    private static readonly string AppName = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? "Invoice Generator";

    private static GeneratorSettings SettingsWithThreadSeed(GeneratorSettings s) => new()
    {
        Locale = s.Locale,
        Currency = s.Currency,
        DefaultVatRate = s.DefaultVatRate,
        FixedItemCount = s.FixedItemCount,
        SaveJsonSidecar = s.SaveJsonSidecar,
        Seed = unchecked(s.Seed + Environment.CurrentManagedThreadId * 7919)
    };

    private static readonly NameTable _xmlNameTable = new();

    private static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    private static readonly string InvoiceFolder = Path.Combine(DesktopPath, "Invoices");

    private static readonly GeneratorSettings Settings = GeneratorSettings.Load();

    private static void Main()
    {
        // Set console title and encoding
        Console.Title = $"{AppName} v{AppVersion}";
        Console.OutputEncoding = Encoding.UTF8;

        // Performance: Enable large object heap compaction on Gen2 collection
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

        Directory.CreateDirectory(InvoiceFolder);
        L.Use(Settings.Locale);
        AnsiConsole.Clear();
        Banner();

        while (true)
        {
            // Professionelles Menü ohne Emojis
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold deepskyblue1]┌─────────────────────────────────────────┐[/]\n" +
                           "[bold deepskyblue1]│[/]  [bold cyan]Select an Action[/]                       [bold deepskyblue1]│[/]\n" +
                           "[bold deepskyblue1]└─────────────────────────────────────────┘[/]")
                    .PageSize(14)
                    .HighlightStyle(new Style(Color.Cyan1, Color.Grey15))
                    .AddChoices(
                        $"[cyan]>[/] {L.T("Generate multiple invoices")}",
                        $"[green]>[/] {L.T("Generate one invoice (use DateTime.Now)")}",
                        $"[yellow]>[/] {L.T("Export CSV summary")}",
                        $"[grey]────────────────────────────[/]",
                        $"[blue]>[/] {L.T("Show folder statistics")}",
                        $"[magenta]>[/] {L.T("Find invoices")}",
                        $"[orange1]>[/] {L.T("Validate invoices (integrity)")}",
                        $"[yellow]>[/] {L.T("Aging report (overdue buckets)")}",
                        $"[green]>[/] {L.T("Top customers by revenue")}",
                        $"[grey]────────────────────────────[/]",
                        $"[cyan]>[/] {L.T("Open invoices folder")}",
                        $"[blue]>[/] {L.T("Settings")}",
                        $"[red]>[/] {L.T("Delete All Invoices")}",
                        $"[grey]x[/] {L.T("Exit")}"
                    ));


            // Behandle Trennlinien
            if (choice.Contains("────"))
            {
                continue;
            }

            // Entferne Prefix für Switch-Case
            var cleanChoice = LogLinePrefixRegex().Replace(choice, "").Trim();
            if (cleanChoice == L.T("Generate multiple invoices"))
            {
                GenerateMultiple();
            }
            else if (cleanChoice == L.T("Generate one invoice (use DateTime.Now)"))
            {
                GenerateSingle();
            }
            else if (cleanChoice == L.T("Export CSV summary"))
            {
                ExportCsv();
            }
            else if (cleanChoice == L.T("Delete All Invoices"))
            {
                DeleteInvoices();
            }
            else if (cleanChoice == L.T("Find invoices"))
            {
                FindInvoicesInteractive();
            }
            else if (cleanChoice == L.T("Validate invoices (integrity)"))
            {
                ValidateInvoices();
            }
            else if (cleanChoice == L.T("Aging report (overdue buckets)"))
            {
                AgingReport();
            }
            else if (cleanChoice == L.T("Top customers by revenue"))
            {
                TopCustomersReport();
            }
            else if (cleanChoice == L.T("Show folder statistics"))
            {
                ShowStats();
            }
            else if (cleanChoice == L.T("Open invoices folder"))
            {
                OpenFolder();
            }
            else if (cleanChoice == L.T("Settings"))
            {
                SettingsMenu();
            }
            else if (cleanChoice == L.T("Exit"))
            {
                var exitPanel = new Panel(new Markup("[bold yellow]Thank you for using Invoice Generator![/]"))
                {
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(2, 1)
                };
                AnsiConsole.Write(exitPanel);
                return;
            }
        }
    }

    private static void Banner()
    {
        // Professionelles Banner ohne Emojis
        var rule = new Rule("[bold cyan]═══════════════════════════════════════════════════════════════[/]")
        {
            Style = Style.Parse("cyan"),
            Justification = Justify.Center
        };
        AnsiConsole.Write(rule);

        var banner = new FigletText("INVOICE")
            .Centered()
            .Color(Color.Cyan1);
        AnsiConsole.Write(banner);

        var banner2 = new FigletText("GENERATOR")
            .Centered()
            .Color(Color.DeepSkyBlue1);
        AnsiConsole.Write(banner2);

        AnsiConsole.Write(rule);

        // Info-Panel mit professionellem Design + Version
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn(new GridColumn().PadLeft(2));

        grid.AddRow(
            new Markup("[grey]Version:[/]"),
            new Markup($"[cyan]{AppVersion}[/]")
        );

        grid.AddRow(
            new Markup("[grey]Folder:[/]"),
            new Markup($"[yellow]{InvoiceFolder}[/]")
        );

        grid.AddRow(
            new Markup("[grey]Runtime:[/]"),
            new Markup($"[grey].NET {Environment.Version.Major} | C# 14[/]")
        );

        grid.AddRow(
            new Markup("[grey]Threads:[/]"),
            new Markup($"[grey]{Environment.ProcessorCount} cores[/]")
        );

        var panel = new Panel(grid)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(2, 0, 2, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    // ---------- Generators ----------
    private static void GenerateMultiple()
    {
        // Professioneller Header
        AnsiConsole.Write(new Rule("[bold cyan]Batch Invoice Generation[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var count = AnsiConsole.Prompt(
            new TextPrompt<int>($"[green]{L.T("How many invoices to generate?")}[/]")
                .PromptStyle("cyan")
                .Validate(n => n > 0 ? ValidationResult.Success() : ValidationResult.Error($"[red]ERROR: {L.T("Must be > 0.")}[/]"))
        );

        var fixedItems = AnsiConsole.Prompt(
            new TextPrompt<int?>($"[green]{L.T("Fixed line items per invoice?")}[/] [grey](Enter for {Settings.MinLineItems}-{Settings.MaxLineItems})[/]")
                .DefaultValue(null)
                .AllowEmpty()
                .PromptStyle("cyan")
                .Validate(n => n is null or >= 1 and <= 100000
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[red]ERROR: {L.T("Enter 1..100000 or leave empty.")}[/]"))
        );

        var useJson = AnsiConsole.Confirm($"[green]{L.T("Also save JSON sidecar?")}[/]", Settings.SaveJsonSidecar);
        Settings.FixedItemCount = fixedItems;
        Settings.SaveJsonSidecar = useJson;
        Settings.Save(); // Auto-Save

        AnsiConsole.WriteLine();

        // Optimierung: Vorallokierung von Faker-Instanzen pro Thread (mit Limit)
        var threadCount = Environment.ProcessorCount;
        var targetPoolSize = Math.Min(threadCount * 2, MaxFakerPoolSize);

        for (var i = _fakerPool.Count; i < targetPoolSize; i++)
        {
            _fakerPool.Add(InvoiceFakerFactory.Create(SettingsWithThreadSeed(Settings)));
        }

        var progress = 0;
        var errors = 0;
        var sw = Stopwatch.StartNew();

        // Optimierung: Größerer Batch für bessere Cache-Locality
        var batchSize = Math.Max(1, count / (threadCount * 4));

        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn()
                {
                    CompletedStyle = new Style(Color.Cyan1),
                    RemainingStyle = new Style(Color.Grey23)
                },
                new PercentageColumn() { Style = new Style(Color.Cyan1) },
                new RemainingTimeColumn() { Style = new Style(Color.Yellow) },
                new SpinnerColumn(Spinner.Known.Dots) { Style = new Style(Color.Cyan1) },
            ])
            .Start(ctx =>
            {
                var task = ctx.AddTask(
                    $"[cyan]{L.T("Generating invoices")}[/]",
                    new ProgressTaskSettings { MaxValue = count }
                );

                // Optimierung: Partitioner für besseres Work-Stealing
                var partitioner = Partitioner.Create(0, count, batchSize);

                Parallel.ForEach(partitioner,
                    new ParallelOptions { MaxDegreeOfParallelism = threadCount },
                    (range, loopState) =>
                    {
                        // Thread-lokaler Faker (verhindert Lock-Contention)
                        if (!_fakerPool.TryTake(out var faker))
                        {
                            faker = InvoiceFakerFactory.Create(SettingsWithThreadSeed(Settings));
                        }

                        try
                        {
                            // Batch-Verarbeitung für bessere Cache-Performance
                            for (var i = range.Item1; i < range.Item2; i++)
                            {
                                try
                                {
                                    var inv = faker.Generate();
                                    var nr = Interlocked.Increment(ref _invoiceCounter);
                                    inv.InvoiceNumber = MakeInvoiceNumber(nr);

                                    // I/O-Throttling mit SemaphoreSlim
                                    _ioGate.Wait();
                                    try
                                    {
                                        SaveInvoiceFast(inv, useNow: false, saveJson: useJson);
                                    }
                                    finally
                                    {
                                        _ioGate.Release();
                                    }

                                    var step = Interlocked.Increment(ref progress);

                                    // Optimierung: Weniger häufige UI-Updates (alle 128 statt 64)
                                    if ((step & 0x7F) == 0)
                                    {
                                        task.Value = step;
                                        var elapsed = sw.Elapsed.TotalSeconds;
                                        var rate = step / elapsed;
                                        task.Description = $"[cyan]{L.T("Generating invoices")}[/] [grey]{step}/{count}[/] [yellow]({rate:F0}/s)[/]";
                                    }
                                }
                                catch (Exception)
                                {
                                    Interlocked.Increment(ref errors);
                                }
                            }
                        }
                        finally
                        {
                            _fakerPool.Add(faker);
                        }
                    });

                sw.Stop();
                task.Value = count;
                var totalRate = count / sw.Elapsed.TotalSeconds;
                task.Description = $"[green]SUCCESS: {L.T("All invoices generated.")}[/] [grey]{count} files in {sw.Elapsed.TotalSeconds:F1}s[/] [yellow]({totalRate:F0}/s)[/]";
            });

        // Success Panel
        var resultPanel = new Panel(
            Align.Left(new Markup(
                $"[green]STATUS: {L.T("All invoices generated.")}[/]\n" +
                $"[grey]Files:[/]     [cyan]{count}[/]\n" +
                $"[grey]Time:[/]      [yellow]{sw.Elapsed.TotalSeconds:F1}s[/]\n" +
                $"[grey]Speed:[/]     [yellow]{count / sw.Elapsed.TotalSeconds:F0} invoices/s[/]" +
                (errors > 0 ? $"\n[grey]Errors:[/]    [red]{errors}[/]" : ""))))
        {
            Header = new PanelHeader("[bold green]Generation Complete[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(resultPanel);
        Pause();
    }

    private static string MakeInvoiceNumber(int nr)
    {
        var utc = DateTime.UtcNow;
        var prefix = Settings.InvoicePrefix;

        // Berechne die finale Länge: Prefix + "-" + Datum (8) + "-" + Nummer (6)
        var totalLength = prefix.Length + 1 + 8 + 1 + 6;

        return string.Create(totalLength, (utc, nr, prefix), static (span, state) =>
        {
            var (date, num, pre) = state;

            // Prefix kopieren
            pre.AsSpan().CopyTo(span);
            var pos = pre.Length;

            // Trennstrich
            span[pos++] = '-';

            var year = date.Year;
            var month = date.Month;
            var day = date.Day;

            // Jahr (4 Ziffern)
            span[pos++] = (char)('0' + year / 1000);
            span[pos++] = (char)('0' + year / 100 % 10);
            span[pos++] = (char)('0' + year / 10 % 10);
            span[pos++] = (char)('0' + year % 10);

            // Monat (2 Ziffern)
            span[pos++] = (char)('0' + month / 10);
            span[pos++] = (char)('0' + month % 10);

            // Tag (2 Ziffern)
            span[pos++] = (char)('0' + day / 10);
            span[pos++] = (char)('0' + day % 10);

            // Trennstrich
            span[pos++] = '-';

            // Nummer (6 Ziffern, rechtsbündig mit führenden Nullen)
            var numStr = num.ToString();
            var offset = pos + (6 - numStr.Length);
            for (var i = pos; i < offset; i++)
            {
                span[i] = '0';
            }

            numStr.AsSpan().CopyTo(span[offset..]);
        });
    }

    private static void GenerateSingle()
    {
        AnsiConsole.Write(new Rule("[bold green]Single Invoice Generation[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var useJson = AnsiConsole.Confirm($"[green]{L.T("Also save JSON sidecar?")}[/]", Settings.SaveJsonSidecar);

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .Start($"[cyan]{L.T("Generating one invoice...")}[/]", ctx =>
            {
                var faker = InvoiceFakerFactory.Create(SettingsWithThreadSeed(Settings));
                var inv = faker.Generate();

                var nr = Interlocked.Increment(ref _invoiceCounter);
                inv.InvoiceNumber = MakeInvoiceNumber(nr);

                SaveInvoiceFast(inv, useNow: true, saveJson: useJson);

                Thread.Sleep(300);
            });

        var successPanel = new Panel(
            Align.Left(new Markup(
                $"[green]STATUS: {L.T("Invoice created.")}[/]\n" +
                $"[grey]Format:[/]   [cyan]XML{(useJson ? " + JSON" : "")}[/]\n" +
                $"[grey]Location:[/] [yellow]{InvoiceFolder}[/]")))
        {
            Header = new PanelHeader("[bold green]Success[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(successPanel);
        Pause();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static readonly XmlWriterSettings XmlFast = new()
    {
        Indent = false,
        OmitXmlDeclaration = false,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Async = false,
        NewLineHandling = NewLineHandling.None,
        CheckCharacters = false,
        CloseOutput = true,
        ConformanceLevel = ConformanceLevel.Document
    };

    private static readonly XmlWriterSettings XmlPretty = new()
    {
        Indent = true,
        IndentChars = "  ",
        NewLineOnAttributes = false,
        NewLineChars = "\n",
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        OmitXmlDeclaration = false
    };

    private static void SaveInvoiceFast(Invoice inv, bool useNow, bool saveJson, bool pretty = false)
    {
        var xmlPath = Path.Combine(InvoiceFolder, $"Invoice_{inv.InvoiceNumber}_{inv.IssueDate:yyyyMMdd}.xml");

        // Optimiert: Dynamische Buffer-Größe basierend auf Item-Anzahl
        var estimatedSize = inv.Items.Count * 256; // ~256 bytes pro Item
        var bufferSize = Math.Clamp(estimatedSize, 32 * 1024, 256 * 1024); // 32KB - 256KB

        // Verwende PrettyPrintXml aus Settings wenn nicht explizit überschrieben
        var shouldPrettyPrint = pretty || Settings.PrettyPrintXml;

        using (var fs = new FileStream(xmlPath, new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan | FileOptions.WriteThrough,
            BufferSize = bufferSize,
            PreallocationSize = bufferSize
        }))
        using (var xw = XmlWriter.Create(fs, shouldPrettyPrint ? XmlPretty : XmlFast))
        {
            WriteInvoiceXml(inv, xw);
        }

        if (saveJson)
        {
            var jsonPath = Path.ChangeExtension(xmlPath, ".json");
            using var jfs = new FileStream(jsonPath, new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.Create,
                Share = FileShare.Read,
                Options = FileOptions.SequentialScan | FileOptions.WriteThrough,
                BufferSize = bufferSize / 2,
                PreallocationSize = bufferSize / 2
            });
            JsonSerializer.Serialize(jfs, inv, JsonOpts);
        }

        // Timestamp-Handling optimiert
        DateTime stamp;
        if (useNow)
        {
            stamp = DateTime.Now;
        }
        else
        {
            var minutes = Random.Shared.NextInt64(0, 5L * 365 * 24 * 60);
            stamp = DateTime.Now.AddMinutes(-minutes);
        }

        // Batch-Update der Timestamps (nur einmal pro Datei)
        File.SetCreationTime(xmlPath, stamp);
        File.SetLastWriteTime(xmlPath, stamp);

        if (saveJson)
        {
            var jsonPath = Path.ChangeExtension(xmlPath, ".json");
            File.SetCreationTime(jsonPath, stamp);
            File.SetLastWriteTime(jsonPath, stamp);
        }
    }

    // Schreiblogik: minimal sauberer, WriteElementString wo’s geht.
    // Datums- und Zahlenformatierung strikt invariant, damit Parser nicht die Krise kriegen.
    private static void WriteInvoiceXml(Invoice inv, XmlWriter xw)
    {
        xw.WriteStartDocument();
        xw.WriteStartElement("Invoice");

        // Header
        xw.WriteElementString("InvoiceNumber", inv.InvoiceNumber);
        xw.WriteElementString("IssueDate", inv.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        xw.WriteElementString("DueDate", inv.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
        xw.WriteElementString("Currency", inv.Currency);
        xw.WriteElementString("PaymentTerms", inv.PaymentTerms);

        // Customer
        xw.WriteStartElement("Customer");
        xw.WriteElementString("Name", inv.BillTo.Name);
        xw.WriteElementString("Email", inv.BillTo.Email);

        xw.WriteStartElement("Address");
        xw.WriteElementString("Street", inv.BillTo.Address.Street);
        xw.WriteElementString("ZipCode", inv.BillTo.Address.ZipCode);
        xw.WriteElementString("City", inv.BillTo.Address.City);
        xw.WriteElementString("Country", inv.BillTo.Address.Country);
        xw.WriteEndElement(); // Address
        xw.WriteEndElement(); // Customer

        // Financials
        xw.WriteStartElement("Financials");
        xw.WriteElementString("Subtotal", inv.Subtotal.ToString(CultureInfo.InvariantCulture));
        xw.WriteElementString("StandardTaxRate", inv.TaxRate.ToString(CultureInfo.InvariantCulture));
        xw.WriteElementString("TaxAmount", inv.TaxAmount.ToString(CultureInfo.InvariantCulture));
        xw.WriteElementString("Total", inv.Total.ToString(CultureInfo.InvariantCulture));
        xw.WriteElementString("IBAN", inv.Iban);
        xw.WriteElementString("BIC", inv.Bic);
        xw.WriteElementString("VendorVAT", inv.VendorVatId);
        xw.WriteElementString("CustomerVAT", inv.CustomerVatId);
        xw.WriteEndElement(); // Financials

        // Items
        xw.WriteStartElement("Items");
        foreach (var li in inv.Items)
        {
            xw.WriteStartElement("Item");
            xw.WriteElementString("SKU", li.Sku);
            xw.WriteElementString("Description", li.Description);
            xw.WriteElementString("Quantity", li.Qty.ToString(CultureInfo.InvariantCulture));
            xw.WriteElementString("UnitPrice", li.UnitPrice.ToString(CultureInfo.InvariantCulture));
            xw.WriteElementString("VATRate", li.VatRate.ToString(CultureInfo.InvariantCulture));
            xw.WriteElementString("LineTotal", li.LineTotal.ToString(CultureInfo.InvariantCulture));
            xw.WriteEndElement(); // Item
        }
        xw.WriteEndElement(); // Items

        // Notes
        xw.WriteElementString("Notes", inv.Notes);

        xw.WriteEndElement();   // Invoice
        xw.WriteEndDocument();
    }

    // ---------- CSV / Search / Stats ----------
    private static void ExportCsv()
    {
        AnsiConsole.Write(new Rule("[bold yellow]CSV Export[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var csvPath = Path.Combine(InvoiceFolder, $"Invoices_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sw = Stopwatch.StartNew();
        var files = Directory.EnumerateFiles(InvoiceFolder, "Invoice_*.xml").ToArray();

        if (files.Length == 0)
        {
            var emptyPanel = new Panel("[yellow]WARNING: No invoices found to export[/]")
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(emptyPanel);
            Pause();
            return;
        }

        var lines = new ConcurrentBag<string>();
        var processed = 0;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .Start($"[yellow]Scanning {files.Length} invoices...[/]", ctx =>
            {
                var partitioner = Partitioner.Create(files, EnumerablePartitionerOptions.NoBuffering);

                Parallel.ForEach(partitioner,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    f =>
                    {
                        try
                        {
                            var doc = XDocument.Load(f, LoadOptions.None);
                            var root = doc.Root;
                            if (root == null)
                            {
                                return;
                            }

                            var invNo = root.Element("InvoiceNumber")?.Value ?? "";
                            var issue = root.Element("IssueDate")?.Value ?? "";
                            var due = root.Element("DueDate")?.Value ?? "";
                            var cust = root.Element("Customer")?.Element("Name")?.Value ?? "";
                            var currency = root.Element("Currency")?.Value ?? "";
                            var financials = root.Element("Financials");
                            var subtotal = financials?.Element("Subtotal")?.Value ?? "0";
                            var tax = financials?.Element("TaxAmount")?.Value ?? "0";
                            var total = financials?.Element("Total")?.Value ?? "0";

                            var line = $"{invNo},{issue},{due},{EscapeCsv(cust)},{subtotal},{tax},{total},{currency}";
                            lines.Add(line);

                            var count = Interlocked.Increment(ref processed);
                            if ((count & 0x3F) == 0)
                            {
                                ctx.Status($"[yellow]Processed {count}/{files.Length} invoices...[/]");
                            }
                        }
                        catch { /* ignore */ }
                    });

                ctx.Status($"[green]Writing CSV file...[/]");
            });

        using (var fs = new FileStream(csvPath, new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan,
            BufferSize = 512 * 1024
        }))
        using (var writer = new StreamWriter(fs, new UTF8Encoding(false), 256 * 1024))
        {
            writer.WriteLine("InvoiceNumber,IssueDate,DueDate,Customer,Subtotal,TaxAmount,Total,Currency");
            foreach (var line in lines.OrderBy(l => l))
            {
                writer.WriteLine(line);
            }
        }

        sw.Stop();
        var rate = files.Length / sw.Elapsed.TotalSeconds;

        var resultPanel = new Panel(
            Align.Left(new Markup(
                $"[green]STATUS: CSV exported successfully[/]\n" +
                $"[grey]File:[/]     [cyan]{Path.GetFileName(csvPath)}[/]\n" +
                $"[grey]Invoices:[/] [yellow]{files.Length}[/]\n" +
                $"[grey]Time:[/]     [yellow]{sw.Elapsed.TotalSeconds:F1}s[/]\n" +
                $"[grey]Speed:[/]    [yellow]{rate:F0} invoices/s[/]\n" +
                $"[grey]Path:[/]     [blue]{csvPath}[/]")))
        {
            Header = new PanelHeader("[bold green]Export Complete[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(resultPanel);
        Pause();
    }

    private static void DeleteInvoices()
    {
        AnsiConsole.Write(new Rule("[bold red]Delete All Invoices[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var files = Directory.GetFiles(InvoiceFolder, "*");
        if (files.Length == 0)
        {
            var emptyPanel = new Panel("[yellow]No invoices found to delete[/]")
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(emptyPanel);
            Pause();
            return;
        }

        // Warning Panel
        var warningPanel = new Panel(
            Align.Center(new Markup(
                $"[bold red]WARNING[/]\n\n" +
                $"You are about to delete [yellow]{files.Length}[/] files!\n" +
                $"This action [red]cannot be undone[/].")))
        {
            Border = BoxBorder.Heavy,
            BorderStyle = new Style(Color.Red),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(warningPanel);
        AnsiConsole.WriteLine();

        var confirm = AnsiConsole.Confirm($"[bold red]{L.T("Are you sure you want to delete all invoices?")}[/]", false);
        if (!confirm)
        {
            AnsiConsole.MarkupLine($"[yellow]{L.T("Deletion process canceled.")}[/]");
            Pause();
            return;
        }

        var deleted = 0;
        var errors = 0;

        AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn()
                {
                    CompletedStyle = new Style(Color.Red),
                    RemainingStyle = new Style(Color.Grey23)
                },
                new PercentageColumn() { Style = new Style(Color.Red) },
                new SpinnerColumn(Spinner.Known.Dots) { Style = new Style(Color.Red) },
            ])
            .Start(ctx =>
            {
                var task = ctx.AddTask($"[red]{L.T("Delete invoices")}[/]", new ProgressTaskSettings { MaxValue = files.Length });

                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
                {
                    try
                    {
                        File.Delete(file);
                        var current = Interlocked.Increment(ref deleted);

                        if ((current & 0x3F) == 0)
                        {
                            task.Value = current;
                            task.Description = $"[red]Deleting[/] [grey]{current}/{files.Length}[/]";
                        }
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref errors);
                    }
                });

                task.Value = files.Length;
                task.Description = $"[green]Deletion complete[/] [grey]{deleted} deleted, {errors} errors[/]";
            });

        var resultPanel = new Panel(
            Align.Left(new Markup(
                $"[green]STATUS: {L.T("All invoices have been deleted.")}[/]\n" +
                $"[grey]Deleted:[/] [cyan]{deleted}[/]\n" +
                (errors > 0 ? $"[grey]Errors:[/]  [red]{errors}[/]" : "[grey]Errors:[/]  [green]0[/]"))))
        {
            Header = new PanelHeader("[bold green]Deletion Complete[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = errors > 0 ? new Style(Color.Yellow) : new Style(Color.Green),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(resultPanel);
        Pause();
    }

    private static void ShowStats()
    {
        var scans = ScanInvoices(InvoiceFolder);
        var files = scans.ToList();

        var table = new Table().Centered().Title($"[bold cyan]{L.T("Folder Overview")}[/]");
        table.AddColumn(L.T("Metric"));
        table.AddColumn(L.T("Value"));

        var totalSize = files.Sum(f => f.FileBytes);
        table.AddRow(L.T("Invoice files (XML)"), files.Count.ToString());
        table.AddRow(L.T("Total size (MB)"), (totalSize / 1024d / 1024d).ToString("F2"));

        if (files.Count != 0)
        {
            var oldest = files.Min(f => f.LastWriteUtc).ToLocalTime();
            var newest = files.Max(f => f.LastWriteUtc).ToLocalTime();
            table.AddRow(L.T("Oldest file"), oldest.ToString("yyyy-MM-dd HH:mm:ss"));
            table.AddRow(L.T("Newest file"), newest.ToString("yyyy-MM-dd HH:mm:ss"));
            table.AddRow(L.T("Median file size (KB)"), (Median(files.Select(f => f.FileBytes)) / 1024d).ToString("F2"));
            table.AddRow(L.T("JSON sidecars"), files.Count(f => f.HasJsonSidecar).ToString());
        }
        AnsiConsole.Write(table);

        if (files.Count == 0)
        {
            Pause();
            return;
        }

        // 1) Currency breakdown
        var byCurrency = files
            .GroupBy(f => f.Currency)
            .Select(g => new { Currency = g.Key, Count = g.Count(), Total = g.Sum(x => x.Total) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var curTable = new Table().Title($"[bold]{L.T("Totals by Currency")}[/]");
        curTable.AddColumn(L.T("Currency"));
        curTable.AddColumn(L.T("Invoices"));
        curTable.AddColumn(L.T("Sum Total"));
        foreach (var c in byCurrency)
        {
            curTable.AddRow(c.Currency, c.Count.ToString(), c.Total.ToString("N2"));
        }

        AnsiConsole.Write(curTable);

        // 2) VAT buckets (über alle enthaltenen per-line VATs)
        var vatPairs = files
            .SelectMany(f => f.VatRates.Select(r => (Rate: r, f.Total)))
            .GroupBy(x => x.Rate)
            .Select(g => new { Rate = g.Key, TotalCount = g.Count(), InvoiceHits = g.Select(x => x.Total).Count(), })
            .OrderByDescending(x => x.Rate)
            .ToList();

        var vatTable = new Table().Title($"[bold]{L.T("VAT Rates (presence across line items)")}[/]");
        vatTable.AddColumn(L.T("VAT Rate"));
        vatTable.AddColumn(L.T("Occurrences (items)"));
        vatTable.AddColumn(L.T("Invoices touched"));
        foreach (var v in vatPairs)
        {
            vatTable.AddRow($"{v.Rate:P0}", v.TotalCount.ToString(), v.InvoiceHits.ToString());
        }

        AnsiConsole.Write(vatTable);

        // 3) Histogram nach IssueDate (letzte 30 Tage, pro Tag)
        var from = DateTime.Today.AddDays(-29);
        var dayBuckets = Enumerable.Range(0, 30)
            .Select(d => from.AddDays(d))
            .Select(d =>
            {
                var n = files.Count(f => f.IssueDate.Date == d.Date);
                var sum = files.Where(f => f.IssueDate.Date == d.Date).Sum(f => f.Total);
                return (Day: d, Count: n, Sum: sum);
            })
            .ToList();

        var bars = new BarChart().Width(80).Label($"[bold]{L.T("Invoices per day (last 30 days)")}[/]");
        foreach (var (Day, Count, Sum) in dayBuckets)
        {
            var label = Day.ToString("MM-dd");
            bars.AddItem(label, Count, Color.Green);
        }
        AnsiConsole.Write(bars);

        var perDayTable = new Table().Title($"[bold]{L.T("Daily totals (last 30 days)")}[/]");
        perDayTable.AddColumn(L.T("Day"));
        perDayTable.AddColumn(L.T("#"));
        perDayTable.AddColumn(L.T("Sum"));
        foreach (var (Day, Count, Sum) in dayBuckets.Where(x => x.Count > 0))
        {
            perDayTable.AddRow(Day.ToString("yyyy-MM-dd"), Count.ToString(), Sum.ToString("N2"));
        }

        AnsiConsole.Write(perDayTable);

        // 4) Top 10 customers by revenue
        var topCustomers = files.GroupBy(f => f.Customer)
            .Select(g => new { Customer = g.Key, Count = g.Count(), Total = g.Sum(x => x.Total) })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToList();

        var topTable = new Table().Title($"[bold]{L.T("Top 10 Customers by Revenue")}[/]");
        topTable.AddColumn(L.T("#"));
        topTable.AddColumn(L.T("Customer"));
        topTable.AddColumn(L.T("Invoices"));
        topTable.AddColumn(L.T("Revenue"));
        var rank = 1;
        foreach (var c in topCustomers)
        {
            topTable.AddRow(rank++.ToString(), c.Customer, c.Count.ToString(), c.Total.ToString("N2"));
        }

        AnsiConsole.Write(topTable);

        // 5) Aging Buckets relativ zu DueDate
        var now = DateTime.Now.Date;
        var aging = files.Select(f => new
        {
            f.InvoiceNumber,
            f.Customer,
            DaysOverdue = f.DueDate.HasValue ? (now - f.DueDate.Value.Date).Days : 0,
            f.Total
        }).ToList();

        var bucket = new Func<int, string>(d =>
        {
            if (d <= 0)
            {
                return "Current";
            }

            if (d <= 30)
            {
                return "1–30";
            }

            if (d <= 60)
            {
                return "31–60";
            }

            if (d <= 90)
            {
                return "61–90";
            }

            return "90+";
        });

        var byBucket = aging.GroupBy(a => bucket(a.DaysOverdue))
            .Select(g => new { Bucket = g.Key, Count = g.Count(), Sum = g.Sum(x => x.Total) })
            .OrderBy(b => b.Bucket switch { "Current" => 0, "1–30" => 1, "31–60" => 2, "61–90" => 3, _ => 4 })
            .ToList();

        var agingTable = new Table().Title($"[bold]{L.T("Aging Buckets (by DueDate)")}[/]");
        agingTable.AddColumn(L.T("Bucket"));
        agingTable.AddColumn(L.T("Count"));
        agingTable.AddColumn(L.T("Total"));
        foreach (var a in byBucket)
        {
            agingTable.AddRow(a.Bucket, a.Count.ToString(), a.Sum.ToString("N2"));
        }

        AnsiConsole.Write(agingTable);

        // 6) Ausreißer: Top 5 highest totals
        var topInvoices = files.OrderByDescending(f => f.Total).Take(5).ToList();
        var outTable = new Table().Title($"[bold]{L.T("Top 5 Invoices by Total")}[/]");
        outTable.AddColumn(L.T("Invoice"));
        outTable.AddColumn(L.T("Customer"));
        outTable.AddColumn(L.T("IssueDate"));
        outTable.AddColumn(L.T("DueDate"));
        outTable.AddColumn(L.T("Total"));
        foreach (var t in topInvoices)
        {
            outTable.AddRow(t.InvoiceNumber, t.Customer, t.IssueDate.ToString("yyyy-MM-dd"),
                t.DueDate?.ToString("yyyy-MM-dd") ?? "-", t.Total.ToString("N2"));
        }

        AnsiConsole.Write(outTable);

        // 7) Anomalien & Duplikate
        var anomalies = ValidateCore(files, tolerance: 0.02m, out var dupes);
        var panel = new Panel(new Markup(
            $"[yellow]{L.T("Duplicates:")}[/] {dupes.Count}\n" +
            $"[yellow]{L.T("Anomalies (sums/tax mismatch):")}[/] {anomalies.Count}\n" +
            $"[yellow]{L.T("Missing DueDate:")}[/] {files.Count(f => f.DueDate == null)}"))
            .Header(L.T("Quick integrity summary"));
        AnsiConsole.Write(panel);

        Pause();
    }

    // ===================================
    // INTERAKTIVE ZUSATZ-FUNKTIONEN
    // ===================================
    private static void FindInvoicesInteractive()
    {
        var term = AnsiConsole.Ask<string>(L.T("Search (customer/invoice no., case-insensitive):"));
        if (string.IsNullOrWhiteSpace(term))
        {
            return;
        }

        var scans = ScanInvoices(InvoiceFolder)
            .Where(f => f.Customer.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || f.InvoiceNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.IssueDate)
            .Take(50)
            .ToList();

        if (scans.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{L.T("No matches.")}[/]");
            Pause();
            return;
        }

        var t = new Table().Title($"[bold]{string.Format(L.T("Results ({0})"), scans.Count)}[/]");
        t.AddColumn(L.T("Invoice"));
        t.AddColumn(L.T("Customer"));
        t.AddColumn(L.T("Issue"));
        t.AddColumn(L.T("Due"));
        t.AddColumn(L.T("Total"));
        t.AddColumn(L.T("Path"));
        foreach (var s in scans)
        {
            t.AddRow(s.InvoiceNumber, s.Customer, s.IssueDate.ToString("yyyy-MM-dd"),
                s.DueDate?.ToString("yyyy-MM-dd") ?? "-", s.Total.ToString("N2"), s.Path);
        }

        AnsiConsole.Write(t);
        Pause();
    }

    private static void ValidateInvoices()
    {
        var scans = ScanInvoices(InvoiceFolder).ToList();
        var anomalies = ValidateCore(scans, tolerance: 0.02m, out var dupes);

        var tree = new Tree($"[bold]{L.T("Validation Report")}[/]");

        // Anzeige der Duplikate
        if (dupes.Count > 0)
        {
            tree.AddNode($"{L.T("Duplicates:")} {dupes.Count}");
            foreach (var d in dupes)
            {
                tree.AddNode($"[red]{d.Key}[/] -> {d.Count()} files");
            }
        }
        else
        {
            tree.AddNode("[green]No duplicates found.[/]");

        }

        // Anzeige der Anomalien
        if (anomalies.Count > 0)
        {
            var an = tree.AddNode(string.Format(L.T("Anomalies (first 20 of {0}):"), anomalies.Count));
            foreach (var a in anomalies.Take(20))
            {
                an.AddNode($"[yellow]{a.Code}[/] {a.Message}");
            }
        }
        else
        {
            tree.AddNode("[green]No anomalies found.[/]");
        }

        AnsiConsole.Write(tree);

        // optional: CSV ablegen
        var csv = Path.Combine(InvoiceFolder, $"Validation_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        using (var sw = new StreamWriter(csv, false, new UTF8Encoding(true)))
        {
            sw.WriteLine("Type,Invoice,Message,Path");
            foreach (var d in dupes)
            {
                sw.WriteLine($"Duplicate,{d.Key},count={d.Count()},");
            }

            foreach (var a in anomalies)
            {
                sw.WriteLine($"Anomaly,{a.InvoiceNumber},{EscapeCsv(a.Message)},{a.Path}");
            }
        }
        AnsiConsole.MarkupLine($"[green]{L.T("Validation CSV:")}[/] {csv}");
        Pause();
    }

    private static void AgingReport()
    {
        var scans = ScanInvoices(InvoiceFolder).ToList();
        var now = DateTime.Now.Date;

        var rows = scans.Select(f => new
        {
            f.InvoiceNumber,
            f.Customer,
            Due = f.DueDate,
            DaysOverdue = f.DueDate.HasValue ? (now - f.DueDate.Value.Date).Days : (int?)null,
            f.Total
        })
        .OrderByDescending(x => x.DaysOverdue ?? int.MinValue)
        .ToList();

        var tt = new Table().Title($"[bold]{L.T("Aging Details")}[/]");
        tt.AddColumn(L.T("Invoice"));
        tt.AddColumn(L.T("Customer"));
        tt.AddColumn(L.T("Due"));
        tt.AddColumn(L.T("Days overdue"));
        tt.AddColumn(L.T("Total"));
        foreach (var r in rows.Take(200))
        {
            tt.AddRow(r.InvoiceNumber, r.Customer, r.Due?.ToString("yyyy-MM-dd") ?? "-",
                r.DaysOverdue?.ToString() ?? "-", r.Total.ToString("N2"));
        }

        AnsiConsole.Write(tt);
        Pause();
    }

    private static void TopCustomersReport()
    {
        var scans = ScanInvoices(InvoiceFolder).ToList();
        var groups = scans.GroupBy(f => f.Customer)
            .Select(g => new { Customer = g.Key, Count = g.Count(), Total = g.Sum(x => x.Total), Last = g.Max(x => x.IssueDate) })
            .OrderByDescending(x => x.Total)
            .Take(50)
            .ToList();

        var tc = new Table().Title($"[bold]{L.T("Top Customers")}[/]");
        tc.AddColumn(L.T("#"));
        tc.AddColumn(L.T("Customer"));
        tc.AddColumn(L.T("Invoices"));
        tc.AddColumn(L.T("Revenue"));
        var i = 1;
        foreach (var g in groups)
        {
            tc.AddRow(i++.ToString(), g.Customer, g.Count.ToString(), g.Total.ToString("N2"));
        }

        AnsiConsole.Write(tc);
        Pause();
    }

    // ===================================
    // CORE: Scanner + Validator
    // ===================================
    private static IEnumerable<InvoiceScan> ScanInvoices(string folder)
    {
        var files = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "Invoice_*.xml").ToArray()
            : [];

        if (files.Length == 0)
        {
            return [];
        }

        // Optimiert: Paralleles Scannen mit Partitioner für bessere Load-Balancing
        var results = new ConcurrentBag<InvoiceScan>();
        var partitioner = Partitioner.Create(files, EnumerablePartitionerOptions.NoBuffering);

        Parallel.ForEach(partitioner,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            path =>
            {
                var scan = TryScanInvoice(path);
                if (scan is not null)
                {
                    results.Add(scan);
                }
            });

        return results.OrderBy(s => s.InvoiceNumber);
    }

    private static InvoiceScan? TryScanInvoice(string path)
    {
        try
        {
            // Optimiert: XDocument ist schneller für complettes Parsen
            var doc = XDocument.Load(path, LoadOptions.None);
            var root = doc.Root;
            if (root == null)
            {
                return null;
            }

            var invNo = root.Element("InvoiceNumber")?.Value ?? "";
            var cust = root.Element("Customer")?.Element("Name")?.Value ?? "";
            var currency = root.Element("Currency")?.Value ?? "";

            _ = DateTime.TryParse(root.Element("IssueDate")?.Value, out var issue);
            var hasDue = DateTime.TryParse(root.Element("DueDate")?.Value, out var dueDt);

            var financials = root.Element("Financials");
            var subtotal = Dec(financials?.Element("Subtotal")?.Value);
            var tax = Dec(financials?.Element("TaxAmount")?.Value);
            var total = Dec(financials?.Element("Total")?.Value);

            var items = root.Element("Items")?.Elements("Item") ?? [];
            var itemCount = items.Count();
            var vatSet = new HashSet<decimal>();

            foreach (var item in items)
            {
                var vat = Dec(item.Element("VATRate")?.Value);
                vatSet.Add(vat);
            }

            var fi = new FileInfo(path);
            var jsonSidecar = File.Exists(Path.ChangeExtension(path, ".json"));

            return new InvoiceScan(
                Path: path,
                InvoiceNumber: invNo,
                IssueDate: issue,
                DueDate: hasDue ? dueDt : null,
                Customer: cust,
                Subtotal: subtotal,
                Tax: tax,
                Total: total,
                Currency: currency,
                ItemCount: itemCount,
                VatRates: [.. vatSet],
                HasJsonSidecar: jsonSidecar,
                FileBytes: fi.Length,
                LastWriteUtc: fi.LastWriteTimeUtc
            );
        }
        catch
        {
            return null;
        }
    }

    private static List<(string Code, string InvoiceNumber, string Message, string Path)> ValidateCore(
        List<InvoiceScan> scans, decimal tolerance, out List<IGrouping<string, InvoiceScan>> duplicates)
    {
        // Duplikate nach InvoiceNumber
        duplicates = [.. scans.Where(s => !string.IsNullOrWhiteSpace(s.InvoiceNumber))
                          .GroupBy(s => s.InvoiceNumber)
                          .Where(g => g.Count() > 1)];

        var anomalies = new List<(string Code, string InvoiceNumber, string Message, string Path)>();

        foreach (var s in scans)
        {
            // Negativwerte
            if (s.Subtotal < 0 || s.Tax < 0 || s.Total < 0)
            {
                anomalies.Add(("NEG", s.InvoiceNumber, "Negative amounts detected", s.Path));
            }

            // Total ≈ Subtotal + Tax (tolerant)
            var diff = Math.Abs(s.Total - (s.Subtotal + s.Tax));
            if (diff > tolerance)
            {
                anomalies.Add(("SUM", s.InvoiceNumber, $"Total != Subtotal+Tax (Δ={diff:N2})", s.Path));
            }

            // Unplausible VAT wenn keine VAT-Rates aber Tax > 0
            if ((s.VatRates == null || s.VatRates.Count == 0) && s.Tax > 0)
            {
                anomalies.Add(("VAT", s.InvoiceNumber, "Tax > 0 but no per-line VAT rates found", s.Path));
            }
        }

        return anomalies;
    }

    // ===================================
    // UTILS
    // ===================================
    private static decimal Dec(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
        {
            return 0m;
        }

        return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d
            : decimal.TryParse(v, NumberStyles.Any, CultureInfo.CurrentCulture, out d) ? d : 0m;
    }

    private static double Median(IEnumerable<long> source)
    {
        var arr = source.OrderBy(x => x).ToArray();
        if (arr.Length == 0)
        {
            return 0;
        }

        var mid = arr.Length / 2;
        return arr.Length % 2 == 0 ? (arr[mid - 1] + arr[mid]) / 2.0 : arr[mid];
    }

    private static string EscapeCsv(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        return s;
    }

    // ---------- Settings ----------
    private static void SettingsMenu()
    {
        while (true)
        {
            AnsiConsole.Clear();
            Banner();

            // Erstelle kategorisierte Optionen
            var panel = new Panel(
                Align.Left(new Markup(
                    $"[cyan]Settings File:[/] [grey]{GeneratorSettings.GetSettingsPath()}[/]\n" +
                    $"[yellow]All changes are automatically saved[/]")))
            {
                Header = new PanelHeader("[bold cyan]Configuration Manager[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan),
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold deepskyblue1]Select Setting to Modify[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(Color.Cyan1, Color.Grey15))
                    .AddChoiceGroup("[bold yellow]Regional Settings[/]",
                    [
                        $"  Locale: [cyan]{Settings.Locale}[/]",
                        $"  Currency: [cyan]{Settings.Currency}[/]",
                        $"  Date Format: [cyan]{Settings.DateFormat}[/]"
                    ])
                    .AddChoiceGroup("[bold yellow]Financial Settings[/]",
                    [
                        $" Default VAT Rate: [cyan]{Settings.DefaultVatRate:P0}[/]",
                        $"  Min Unit Price: [cyan]{Settings.MinUnitPrice:C}[/]",
                        $"  Max Unit Price: [cyan]{Settings.MaxUnitPrice:C}[/]",
                        $"  Payment Terms (days): [cyan]{string.Join(", ", Settings.PaymentTermsDays)}[/]"
                    ])
                    .AddChoiceGroup("[bold yellow]Generation Settings[/]",
                    [
                        $"  Invoice Prefix: [cyan]{Settings.InvoicePrefix}[/]",
                        $"  Auto-Increment Number: [cyan]{(Settings.AutoIncrementInvoiceNumber ? "ON" : "OFF")}[/]",
                        $"  Fixed Line Items: [cyan]{Settings.FixedItemCount?.ToString() ?? "auto"}[/]",
                        $"  Min Line Items: [cyan]{Settings.MinLineItems}[/]",
                        $"  Max Line Items: [cyan]{Settings.MaxLineItems}[/]",
                        $"  Random Seed: [cyan]{Settings.Seed}[/]"
                    ])
                    .AddChoiceGroup("[bold yellow]Output Settings[/]",
                    [
                        $"  JSON Sidecar: [cyan]{(Settings.SaveJsonSidecar ? "ON" : "OFF")}[/]",
                        $"  Pretty Print XML: [cyan]{(Settings.PrettyPrintXml ? "ON" : "OFF")}[/]"
                    ])
                    .AddChoices("[grey]« Back to Main Menu[/]")
            );

            // Parse choice
            var cleanChoice = choice.Trim();

            if (cleanChoice.Contains("Locale:"))
            {
                var loc = AnsiConsole.Ask<string>("[cyan]Enter locale (e.g., en, de, fr, en_GB):[/]", Settings.Locale);
                Settings.Locale = string.IsNullOrWhiteSpace(loc) ? Settings.Locale : loc.Trim();
                L.Use(Settings.Locale);
                Settings.Save();
                ShowSuccess("Locale updated");
            }
            else if (cleanChoice.Contains("Currency:"))
            {
                var cur = AnsiConsole.Ask<string>("[cyan]Enter currency code (ISO 4217, e.g., EUR, USD, GBP):[/]", Settings.Currency);
                Settings.Currency = string.IsNullOrWhiteSpace(cur) ? Settings.Currency : cur.Trim().ToUpperInvariant();
                Settings.Save();
                ShowSuccess("Currency updated");
            }
            else if (cleanChoice.Contains("Date Format:"))
            {
                var format = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]Select date format:[/]")
                        .AddChoices("yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd.MM.yyyy"));
                Settings.DateFormat = format;
                Settings.Save();
                ShowSuccess("Date format updated");
            }
            else if (cleanChoice.Contains("Default VAT Rate:"))
            {
                var pct = AnsiConsole.Ask<decimal>("[cyan]Enter VAT as decimal (e.g., 0.19 for 19%):[/]", Settings.DefaultVatRate);
                Settings.DefaultVatRate = Math.Clamp(pct, 0m, 1m);
                Settings.Save();
                ShowSuccess($"VAT rate updated to {Settings.DefaultVatRate:P0}");
            }
            else if (cleanChoice.Contains("Min Unit Price:"))
            {
                var price = AnsiConsole.Ask<decimal>("[cyan]Enter minimum unit price:[/]", Settings.MinUnitPrice);
                Settings.MinUnitPrice = Math.Max(0.01m, price);
                Settings.Save();
                ShowSuccess($"Min unit price updated to {Settings.MinUnitPrice:C}");
            }
            else if (cleanChoice.Contains("Max Unit Price:"))
            {
                var price = AnsiConsole.Ask<decimal>("[cyan]Enter maximum unit price:[/]", Settings.MaxUnitPrice);
                Settings.MaxUnitPrice = Math.Max(Settings.MinUnitPrice, price);
                Settings.Save();
                ShowSuccess($"Max unit price updated to {Settings.MaxUnitPrice:C}");
            }
            else if (cleanChoice.Contains("Payment Terms"))
            {
                var terms = AnsiConsole.Ask<string>("[cyan]Enter payment term days (comma-separated, e.g., 7,14,30):[/]",
                    string.Join(",", Settings.PaymentTermsDays));
                try
                {
                    Settings.PaymentTermsDays = [.. terms.Split(',')
                        .Select(s => int.Parse(s.Trim()))
                        .Where(d => d > 0)];
                    Settings.Save();
                    ShowSuccess($"Payment terms updated to: {string.Join(", ", Settings.PaymentTermsDays)} days");
                }
                catch
                {
                    AnsiConsole.MarkupLine("[red]Invalid format. Please use comma-separated numbers.[/]");
                    Thread.Sleep(2000);
                }
            }
            else if (cleanChoice.Contains("Invoice Prefix:"))
            {
                var prefix = AnsiConsole.Ask<string>("[cyan]Enter invoice prefix (e.g., INV, BILL, INV-):[/]", Settings.InvoicePrefix);
                Settings.InvoicePrefix = string.IsNullOrWhiteSpace(prefix) ? "INV" : prefix.Trim();
                Settings.Save();
                ShowSuccess($"Invoice prefix updated to '{Settings.InvoicePrefix}'");
            }
            else if (cleanChoice.Contains("Auto-Increment Number:"))
            {
                Settings.AutoIncrementInvoiceNumber = AnsiConsole.Confirm("[cyan]Enable auto-increment invoice numbers?[/]", Settings.AutoIncrementInvoiceNumber);
                Settings.Save();
                ShowSuccess($"Auto-increment {(Settings.AutoIncrementInvoiceNumber ? "enabled" : "disabled")}");
            }
            else if (cleanChoice.Contains("Fixed Line Items:"))
            {
                var text = AnsiConsole.Ask<string>("[cyan]Enter fixed line items (1-10000) or leave empty for auto:[/]",
                    Settings.FixedItemCount?.ToString() ?? "");
                if (string.IsNullOrWhiteSpace(text))
                {
                    Settings.FixedItemCount = null;
                    ShowSuccess("Using auto line items (random between min/max)");
                }
                else if (int.TryParse(text, out var n) && n is >= 1 and <= 10000)
                {
                    Settings.FixedItemCount = n;
                    ShowSuccess($"Fixed line items set to {n}");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Invalid number. Must be 1-10000.[/]");
                    Thread.Sleep(2000);
                    continue;
                }
                Settings.Save();
            }
            else if (cleanChoice.Contains("Min Line Items:"))
            {
                var count = AnsiConsole.Ask<int>("[cyan]Enter minimum line items per invoice:[/]", Settings.MinLineItems);
                Settings.MinLineItems = Math.Clamp(count, 1, 10000);
                Settings.Save();
                ShowSuccess($"Min line items updated to {Settings.MinLineItems}");
            }
            else if (cleanChoice.Contains("Max Line Items:"))
            {
                var count = AnsiConsole.Ask<int>("[cyan]Enter maximum line items per invoice:[/]", Settings.MaxLineItems);
                Settings.MaxLineItems = Math.Clamp(count, Settings.MinLineItems, 10000);
                Settings.Save();
                ShowSuccess($"Max line items updated to {Settings.MaxLineItems}");
            }
            else if (cleanChoice.Contains("Random Seed:"))
            {
                var seed = AnsiConsole.Ask<int>("[cyan]Enter random seed (for reproducible generation):[/]", Settings.Seed);
                Settings.Seed = seed;
                Settings.Save();
                ShowSuccess($"Random seed updated to {Settings.Seed}");
            }
            else if (cleanChoice.Contains("JSON Sidecar:"))
            {
                Settings.SaveJsonSidecar = AnsiConsole.Confirm("[cyan]Enable JSON sidecar files?[/]", Settings.SaveJsonSidecar);
                Settings.Save();
                ShowSuccess($"JSON sidecar {(Settings.SaveJsonSidecar ? "enabled" : "disabled")}");
            }
            else if (cleanChoice.Contains("Pretty Print XML:"))
            {
                Settings.PrettyPrintXml = AnsiConsole.Confirm("[cyan]Enable pretty-printed (indented) XML?[/]", Settings.PrettyPrintXml);
                Settings.Save();
                ShowSuccess($"Pretty print XML {(Settings.PrettyPrintXml ? "enabled" : "disabled")}");
            }
            else if (cleanChoice.Contains("Back"))
            {
                break;
            }
        }
    }

    private static void ShowSuccess(string message)
    {
        var panel = new Panel($"[green]{message}[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(1, 0)
        };
        AnsiConsole.Write(panel);
        Thread.Sleep(1500);
    }

    // ---------- Helpers ----------
    private static void OpenFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = InvoiceFolder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(string.Format($"[red]{L.T("Failed to open folder: {0}")}[/]", ex.Message));
        }
    }

    private static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
        AnsiConsole.Clear();
        Banner();
    }

    [GeneratedRegex(@"^\[.*?\]\s*\S+\s*")]
    private static partial System.Text.RegularExpressions.Regex LogLinePrefixRegex();
}
