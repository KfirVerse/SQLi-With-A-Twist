namespace SqliRceLab.Data;

// ============================================================================
//  In-memory FICTIONAL data for the dashboard pages.
//  Nothing here is real. Company names are standard placeholder names
//  (Acme, Contoso, Fabrikam, Globex, Initech, …), all emails use the reserved
//  example.com domain, and nothing touches the database. The only DB-backed
//  (and intentionally vulnerable) surface is /search.
// ============================================================================

public record Campaign(
    int Id,
    string Name,
    string Advertiser,
    string Channel,
    string Status,
    long Impressions,
    long Clicks,
    decimal Spend)
{
    public double Ctr => Impressions == 0 ? 0 : (double)Clicks / Impressions * 100.0;
    public decimal Cpc => Clicks == 0 ? 0 : Math.Round(Spend / Clicks, 2);
}

public static class DemoData
{
    public const string Brand = "Acme Ad Console";
    public const string Org = "Fictional demo data";

    public static readonly List<Campaign> Campaigns = new()
    {
        new(4801, "Summer Splash",       "Contoso Outdoors",  "Search",   "Active", 1_842_310, 38_642, 12_450m),
        new(4802, "Back-to-Desk Bundle", "Fabrikam Office",   "Display",  "Active",   986_540, 14_205,  6_180m),
        new(4803, "Bright Home Launch",  "Globex Home",       "Social",   "Active", 2_410_880, 61_770, 18_920m),
        new(4804, "Trailhead Gear",      "Acme Outdoors",     "Video",    "Paused",   540_120,  7_930,  4_015m),
        new(4805, "Brew & Bloom",        "Initech Kitchen",   "Search",   "Active",   712_400, 20_110,  5_540m),
        new(4806, "Nightowl Audio",      "Vandelay Audio",    "Social",   "Active", 1_305_770, 33_480,  9_870m),
        new(4807, "Coastal Living",      "Widgetco Home",     "Display",  "Ended",    433_990,  5_140,  2_260m),
        new(4808, "Peak Fitness",        "Sample Athletics",  "Video",    "Active", 1_998_210, 45_902, 15_330m),
        new(4809, "Harvest Table",       "Demo Foods",        "Search",   "Paused",   356_720,  9_015,  3_120m),
        new(4810, "Cooling Sale",        "Placeholder Appl.", "Display",  "Active",   878_640, 12_690,  5_005m),
        new(4811, "Watch Reveal",        "Example Time Co.",  "Social",   "Active", 1_540_300, 41_220, 13_640m),
        new(4812, "Bath Refresh",        "Mockingbird Home",  "Search",   "Ended",    291_450,  6_305,  1_980m),
    };

    public static long TotalImpressions => Campaigns.Sum(c => c.Impressions);
    public static long TotalClicks => Campaigns.Sum(c => c.Clicks);
    public static decimal TotalSpend => Campaigns.Sum(c => c.Spend);
    public static double AvgCtr => TotalImpressions == 0 ? 0 : (double)TotalClicks / TotalImpressions * 100.0;
    public static int ActiveCount => Campaigns.Count(c => c.Status == "Active");

    public static readonly (string Day, decimal Amount)[] Spend7d =
    {
        ("Mon", 9_240m), ("Tue", 11_680m), ("Wed", 10_450m), ("Thu", 13_920m),
        ("Fri", 15_270m), ("Sat", 8_530m), ("Sun", 7_190m),
    };

    public static readonly (string Name, decimal Spend, int Pct)[] Channels =
    {
        ("Search",  26_631m, 33),
        ("Social",  42_430m, 38),
        ("Display", 16_452m, 15),
        ("Video",   19_346m, 14),
    };

    public static readonly (string When, string Text)[] Activity =
    {
        ("2m ago",  "Campaign \"Bright Home Launch\" exceeded daily budget by 4%."),
        ("18m ago", "New creative set uploaded to \"Peak Fitness\"."),
        ("1h ago",  "Bid strategy changed to Target CPA on \"Summer Splash\"."),
        ("3h ago",  "\"Coastal Living\" moved to Ended."),
        ("Today",   "Weekly performance report generated (fictional demo data)."),
    };
}
