namespace SqliRceLab.Data;

// ============================================================================
//  In-memory FICTIONAL data for the dashboard pages.
//  None of this touches the database. It exists only to make the app look like
//  a real ad-analytics console. The ONLY database-backed (and intentionally
//  vulnerable) surface is the /search page.  Every value here is invented.
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
    public const string Brand = "AdVantage Console";
    public const string Org = "Northwind Media Group";

    public static readonly List<Campaign> Campaigns = new()
    {
        new(4801, "Summer Splash 2026",      "Cove Outdoors",     "Search",   "Active", 1_842_310, 38_642, 12_450.75m),
        new(4802, "Back-to-Desk Bundle",     "Atlas Office Co.",  "Display",  "Active",   986_540, 14_205,  6_180.20m),
        new(4803, "Aurora Lighting Launch",  "Lumen Home",        "Social",   "Active", 2_410_880, 61_770, 18_920.00m),
        new(4804, "Trailhead Gear Q3",       "Pioneer Outdoors",  "Video",    "Paused",   540_120,  7_930,  4_015.60m),
        new(4805, "Brew & Bloom Spring",     "Verdant Kitchen",   "Search",   "Active",   712_400, 20_110,  5_540.45m),
        new(4806, "Nightowl Audio Drop",     "Lyric Sound",       "Social",   "Active", 1_305_770, 33_480,  9_870.10m),
        new(4807, "Coastal Living Promo",    "Tidal Home",        "Display",  "Ended",    433_990,  5_140,  2_260.00m),
        new(4808, "Peak Fitness New Year",   "Dune Athletics",    "Video",    "Active", 1_998_210, 45_902, 15_330.80m),
        new(4809, "Harvest Table Feature",   "Grove Goods",       "Search",   "Paused",   356_720,  9_015,  3_120.35m),
        new(4810, "Zephyr Cooling Sale",     "Zephyr Appliances", "Display",  "Active",   878_640, 12_690,  5_005.90m),
        new(4811, "Meridian Watch Reveal",   "Meridian Time",     "Social",   "Active", 1_540_300, 41_220, 13_640.25m),
        new(4812, "Basin Bath Refresh",      "Basin & Co.",       "Search",   "Ended",    291_450,  6_305,  1_980.00m),
    };

    public static long TotalImpressions => Campaigns.Sum(c => c.Impressions);
    public static long TotalClicks => Campaigns.Sum(c => c.Clicks);
    public static decimal TotalSpend => Campaigns.Sum(c => c.Spend);
    public static double AvgCtr => TotalImpressions == 0 ? 0 : (double)TotalClicks / TotalImpressions * 100.0;
    public static int ActiveCount => Campaigns.Count(c => c.Status == "Active");

    // 7-day spend trend (fictional), used for the dashboard bar chart.
    public static readonly (string Day, decimal Amount)[] Spend7d =
    {
        ("Mon", 9_240m),
        ("Tue", 11_680m),
        ("Wed", 10_450m),
        ("Thu", 13_920m),
        ("Fri", 15_270m),
        ("Sat", 8_530m),
        ("Sun", 7_190m),
    };

    // Channel mix (fictional), used on the dashboard and reports.
    public static readonly (string Name, decimal Spend, int Pct)[] Channels =
    {
        ("Search",  26_631m, 33),
        ("Social",  42_430m, 38),
        ("Display", 16_452m, 15),
        ("Video",   19_346m, 14),
    };

    // Recent activity feed (fictional).
    public static readonly (string When, string Text)[] Activity =
    {
        ("2m ago",  "Campaign \"Aurora Lighting Launch\" exceeded daily budget by 4%."),
        ("18m ago", "New creative set uploaded to \"Peak Fitness New Year\"."),
        ("1h ago",  "Bid strategy changed to Target CPA on \"Summer Splash 2026\"."),
        ("3h ago",  "\"Coastal Living Promo\" moved to Ended."),
        ("Today",   "Weekly performance report generated for Northwind Media Group."),
    };
}
