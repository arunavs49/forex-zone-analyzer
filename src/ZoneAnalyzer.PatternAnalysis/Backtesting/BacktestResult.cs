using System;
using System.Collections.Generic;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis.Backtesting;

public enum TradeOutcome
{
    Win,      // TP hit
    Loss,     // SL hit (zone broken)
    Timeout,  // Neither hit within TimeoutCandles
    Skipped   // Against trend (when FilterByTrend is on)
}

public class TradeResult
{
    public TradeOutcome Outcome { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public double EntryPrice { get; set; }
    public double? ExitPrice { get; set; }
    public double? ProfitPips { get; set; }
    public double RiskReward { get; set; }
    public ZoneType ZoneType { get; set; }
    public TrendDirection TrendAtFormation { get; set; }
    public bool WithTrend { get; set; }
}

public class BacktestResult
{
    public ZoneConfiguration ZoneConfig { get; set; } = new();
    public TrendConfiguration TrendConfig { get; set; } = new();

    public int TotalZones { get; set; }
    public int MatchedZones { get; set; }
    public int TradedZones { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Timeouts { get; set; }
    public int Skipped { get; set; }

    public double WinRate => TradedZones > 0 ? (double)Wins / TradedZones : 0;
    public double AverageRR { get; set; }

    /// <summary>
    /// Composite score: weighted combination of win rate, zone count, and RR.
    /// Higher is better.
    /// </summary>
    public double Score { get; set; }

    public List<TradeResult> Trades { get; set; } = new();

    public static double ComputeScore(double winRate, int tradedZones, double avgRR, int minZones)
    {
        if (tradedZones < minZones) return 0;

        // Weights: win rate 50%, zone count relevance 25%, avg RR 25%
        // Zone count is capped at 100 for normalization
        var zoneScore = Math.Min(tradedZones, 100) / 100.0;
        var rrScore = Math.Min(avgRR, 3.0) / 3.0; // Cap RR contribution at 3.0

        return winRate * 0.50 + zoneScore * 0.25 + rrScore * 0.25;
    }
}
