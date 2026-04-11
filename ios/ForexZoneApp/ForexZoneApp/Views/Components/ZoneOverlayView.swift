import SwiftUI

/// Renders a zone as a semi-transparent rectangle overlay on the candlestick chart
struct ZoneOverlayView: View {
    let zone: Zone
    let candles: [Candle]
    let priceMin: Double
    let priceMax: Double
    let chartHeight: CGFloat
    let totalCandleWidth: CGFloat
    let offset: CGFloat
    let chartWidth: CGFloat
    let color: Color

    var body: some View {
        let yTop = yPosition(price: zone.baseRangeHigh)
        let yBottom = yPosition(price: zone.baseRangeLow)
        let height = max(yBottom - yTop, 1)

        // Find the candle index where this zone starts
        let startIndex = findCandleIndex(for: zone.startTime)
        let xStart = CGFloat(startIndex) * totalCandleWidth + offset

        // Zone extends from start to end of chart (zones persist until broken)
        let zoneWidth: CGFloat = zone.freshness == .Broken
            ? CGFloat(findCandleIndex(for: zone.endTime) - startIndex + 1) * totalCandleWidth
            : chartWidth - xStart

        let opacity: Double = switch zone.freshness {
        case .Untested: 0.25
        case .Tested: 0.15
        case .Broken: 0.05
        }

        ZStack(alignment: .topLeading) {
            Rectangle()
                .fill(color.opacity(opacity))
                .border(color.opacity(opacity + 0.1), width: 0.5)

            // Zone label
            if zoneWidth > 50 {
                HStack(spacing: 2) {
                    Text(zone.type == .Supply ? "S" : "D")
                        .font(.system(size: 8, weight: .bold, design: .monospaced))
                    if zone.freshness == .Untested {
                        Circle()
                            .fill(color)
                            .frame(width: 4, height: 4)
                    }
                }
                .foregroundStyle(color)
                .padding(2)
            }
        }
        .frame(width: max(0, zoneWidth), height: height)
        .offset(x: xStart, y: yTop)
    }

    private func yPosition(price: Double) -> CGFloat {
        let ratio = (price - priceMin) / (priceMax - priceMin)
        return chartHeight * (1.0 - ratio)
    }

    private func findCandleIndex(for timeStr: String) -> Int {
        guard let targetDate = parseDate(timeStr) else { return 0 }

        for (index, candle) in candles.enumerated() {
            if let candleDate = candle.date, candleDate >= targetDate {
                return max(0, index - 1)
            }
        }
        return max(0, candles.count - 1)
    }

    private func parseDate(_ str: String) -> Date? {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let d = formatter.date(from: str) { return d }
        formatter.formatOptions = [.withInternetDateTime]
        return formatter.date(from: str)
    }
}
