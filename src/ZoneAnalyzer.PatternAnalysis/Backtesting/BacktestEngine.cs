using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeriRemenyi.Oanda.V20.Client.Model;

namespace ZoneAnalyzer.PatternAnalysis.Backtesting;

/// <summary>
/// Evaluates zone trading performance against historical candle data.
/// Strict no-lookahead: trend is evaluated using only candles available at zone formation time.
/// </summary>
public class BacktestEngine
{
    private readonly BacktestConfig _config;

    public BacktestEngine(BacktestConfig? config = null)
    {
        _config = config ?? new BacktestConfig();
    }

    /// <summary>
    /// Run backtest: given precomputed zones and candle data, evaluate each zone as a trade.
    /// Zones are filtered by zoneConfig; trend evaluated per trendConfig using trendCandles.
    /// </summary>
    /// <param name="allZones">All candidate zones found by ZoneFinder.GetAllZones()</param>
    /// <param name="zoneCandles">Zone timeframe candles (sorted by time ascending)</param>
    /// <param name="trendCandles">Higher TF candles for trend evaluation (sorted by time ascending)</param>
    /// <param name="zoneConfig">Zone filter configuration</param>
    /// <param name="trendConfig">Trend detection configuration</param>
    public BacktestResult Evaluate(
        List<Zone> allZones,
        List<Candlestick> zoneCandles,
        List<Candlestick> trendCandles,
        ZoneConfiguration zoneConfig,
        TrendConfiguration trendConfig)
    {
        var result = new BacktestResult
        {
            ZoneConfig = zoneConfig,
            TrendConfig = trendConfig,
            TotalZones = allZones.Count
        };

        // Filter zones by ZoneConfig
        var matchedZones = allZones.Where(z => zoneConfig.IsMatch(z)).ToList();
        result.MatchedZones = matchedZones.Count;

        if (matchedZones.Count < _config.MinZonesForScoring)
        {
            result.Score = 0;
            return result;
        }

        var totalRR = 0.0;

        foreach (var zone in matchedZones)
        {
            var trade = EvaluateZone(zone, zoneCandles, trendCandles, trendConfig);
            result.Trades.Add(trade);

            switch (trade.Outcome)
            {
                case TradeOutcome.Win:
                    result.Wins++;
                    result.TradedZones++;
                    totalRR += trade.RiskReward;
                    break;
                case TradeOutcome.Loss:
                    result.Losses++;
                    result.TradedZones++;
                    totalRR += trade.RiskReward;
                    break;
                case TradeOutcome.Timeout:
                    result.Timeouts++;
                    result.TradedZones++;
                    break;
                case TradeOutcome.Skipped:
                    result.Skipped++;
                    break;
            }
        }

        result.AverageRR = result.TradedZones > 0 ? totalRR / result.TradedZones : 0;
        result.Score = BacktestResult.ComputeScore(
            result.WinRate, result.TradedZones, result.AverageRR, _config.MinZonesForScoring);

        return result;
    }

    private TradeResult EvaluateZone(
        Zone zone,
        List<Candlestick> zoneCandles,
        List<Candlestick> trendCandles,
        TrendConfiguration trendConfig)
    {
        var trade = new TradeResult
        {
            ZoneType = zone.Type,
            EntryTime = zone.EndTime,
        };

        // Evaluate trend at zone formation time (no lookahead)
        var trendAtFormation = GetTrendAtTime(trendCandles, zone.EndTime, trendConfig);
        trade.TrendAtFormation = trendAtFormation;

        // Determine if trade is with-trend
        trade.WithTrend = (zone.Type == ZoneType.Demand && trendAtFormation == TrendDirection.Up)
                       || (zone.Type == ZoneType.Supply && trendAtFormation == TrendDirection.Down);

        if (_config.FilterByTrend && !trade.WithTrend)
        {
            trade.Outcome = TradeOutcome.Skipped;
            return trade;
        }

        // Entry: limit order at zone edge + spread
        var baseWidth = zone.BaseRangeHigh - zone.BaseRangeLow;
        if (baseWidth <= 0)
        {
            trade.Outcome = TradeOutcome.Skipped;
            return trade;
        }

        double entryPrice, stopLoss, takeProfit;

        if (zone.Type == ZoneType.Demand)
        {
            // Buy at zone top (demand: price comes down to zone, we go long)
            entryPrice = zone.BaseRangeHigh + _config.SpreadAssumption;
            stopLoss = zone.BaseRangeLow;
            takeProfit = entryPrice + baseWidth * _config.TakeProfitMultiple;
        }
        else
        {
            // Sell at zone bottom (supply: price comes up to zone, we go short)
            entryPrice = zone.BaseRangeLow - _config.SpreadAssumption;
            stopLoss = zone.BaseRangeHigh;
            takeProfit = entryPrice - baseWidth * _config.TakeProfitMultiple;
        }

        trade.EntryPrice = entryPrice;

        // Walk forward through candles after zone formation
        var candlesAfterZone = zoneCandles
            .Where(c => ParseTime(c) > zone.EndTime)
            .Take(_config.TimeoutCandles)
            .ToList();

        bool entryTriggered = false;

        foreach (var candle in candlesAfterZone)
        {
            var data = candle.GetCandlestickData();

            if (!entryTriggered)
            {
                // Check if price reaches our entry level
                if (zone.Type == ZoneType.Demand && data.L <= entryPrice)
                    entryTriggered = true;
                else if (zone.Type == ZoneType.Supply && data.H >= entryPrice)
                    entryTriggered = true;

                if (!entryTriggered) continue;
                trade.EntryTime = ParseTime(candle);
            }

            // Check SL and TP
            if (zone.Type == ZoneType.Demand)
            {
                if (data.L <= stopLoss)
                {
                    trade.Outcome = TradeOutcome.Loss;
                    trade.ExitPrice = stopLoss;
                    trade.ExitTime = ParseTime(candle);
                    trade.RiskReward = -1.0;
                    trade.ProfitPips = (stopLoss - entryPrice) * PipMultiplier(entryPrice);
                    return trade;
                }
                if (data.H >= takeProfit)
                {
                    trade.Outcome = TradeOutcome.Win;
                    trade.ExitPrice = takeProfit;
                    trade.ExitTime = ParseTime(candle);
                    trade.RiskReward = _config.TakeProfitMultiple;
                    trade.ProfitPips = (takeProfit - entryPrice) * PipMultiplier(entryPrice);
                    return trade;
                }
            }
            else // Supply
            {
                if (data.H >= stopLoss)
                {
                    trade.Outcome = TradeOutcome.Loss;
                    trade.ExitPrice = stopLoss;
                    trade.ExitTime = ParseTime(candle);
                    trade.RiskReward = -1.0;
                    trade.ProfitPips = (entryPrice - stopLoss) * PipMultiplier(entryPrice);
                    return trade;
                }
                if (data.L <= takeProfit)
                {
                    trade.Outcome = TradeOutcome.Win;
                    trade.ExitPrice = takeProfit;
                    trade.ExitTime = ParseTime(candle);
                    trade.RiskReward = _config.TakeProfitMultiple;
                    trade.ProfitPips = (entryPrice - takeProfit) * PipMultiplier(entryPrice);
                    return trade;
                }
            }
        }

        // Neither SL nor TP hit
        trade.Outcome = entryTriggered ? TradeOutcome.Timeout : TradeOutcome.Skipped;
        return trade;
    }

    /// <summary>
    /// Evaluate trend using only candles up to (and including) the given time.
    /// This ensures no lookahead bias.
    /// </summary>
    private static TrendDirection GetTrendAtTime(
        List<Candlestick> trendCandles,
        DateTime atTime,
        TrendConfiguration trendConfig)
    {
        var available = trendCandles
            .Where(c => ParseTime(c) <= atTime)
            .TakeLast(trendConfig.TrendCandleCount)
            .ToList();

        if (available.Count < trendConfig.MinSwingPoints * 2)
            return TrendDirection.Sideways;

        var trendManager = TrendManager.Create(available, trendConfig);
        return trendManager.GetTrendDirection();
    }

    private static DateTime ParseTime(Candlestick c) =>
        DateTime.Parse(c.Time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    /// <summary>Rough pip multiplier based on price level (JPY pairs vs others).</summary>
    private static double PipMultiplier(double price) => price > 10 ? 100 : 10000;
}
