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

        let fillOpacity: Double = switch zone.freshness {
        case .Untested: 0.25
        case .Tested: 0.15
        case .Broken: 0.12
        }

        let borderOpacity: Double = switch zone.freshness {
        case .Untested: 0.6
        case .Tested: 0.4
        case .Broken: 0.35
        }

        let borderStyle: StrokeStyle = zone.freshness == .Broken
            ? StrokeStyle(lineWidth: 1, dash: [4, 3])
            : StrokeStyle(lineWidth: 0.5)

        ZStack(alignment: .topLeading) {
            Rectangle()
                .fill(color.opacity(fillOpacity))
            Rectangle()
                .stroke(color.opacity(borderOpacity), style: borderStyle)

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
        guard let targetDate = parseISO8601(timeStr) else { return 0 }

        for (index, candle) in candles.enumerated() {
            if let candleDate = parseISO8601(candle.time), candleDate >= targetDate {
                return max(0, index - 1)
            }
        }
        return max(0, candles.count - 1)
    }
}
