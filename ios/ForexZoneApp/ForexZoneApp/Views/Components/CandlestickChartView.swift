import SwiftUI

struct CandlestickChartView: View {
    let candles: [Candle]
    let supplyZones: [Zone]
    let demandZones: [Zone]

    @State private var offset: CGFloat = 0
    @State private var scale: CGFloat = 1.0
    @GestureState private var dragOffset: CGFloat = 0
    @GestureState private var magnification: CGFloat = 1.0

    private let candleWidth: CGFloat = 8
    private let candleSpacing: CGFloat = 3

    private var effectiveScale: CGFloat {
        max(0.3, min(scale * magnification, 5.0))
    }

    private var totalCandleWidth: CGFloat {
        (candleWidth + candleSpacing) * effectiveScale
    }

    private var priceRange: (min: Double, max: Double) {
        var lo = Double.infinity
        var hi = -Double.infinity
        for c in candles {
            if let l = c.low { lo = min(lo, l) }
            if let h = c.high { hi = max(hi, h) }
        }
        // Include zone ranges
        for z in supplyZones + demandZones {
            lo = min(lo, z.baseRangeLow)
            hi = max(hi, z.baseRangeHigh)
        }
        let padding = (hi - lo) * 0.05
        return (lo - padding, hi + padding)
    }

    var body: some View {
        GeometryReader { geo in
            let range = priceRange
            let chartHeight = geo.size.height - 40 // leave room for time labels
            let chartWidth = geo.size.width

            ZStack(alignment: .topLeading) {
                // Background
                Color(.systemBackground)

                // Price grid lines
                PriceGridView(
                    priceMin: range.min,
                    priceMax: range.max,
                    chartHeight: chartHeight,
                    chartWidth: chartWidth
                )

                // Zone overlays
                ForEach(demandZones) { zone in
                    ZoneOverlayView(
                        zone: zone,
                        candles: candles,
                        priceMin: range.min,
                        priceMax: range.max,
                        chartHeight: chartHeight,
                        totalCandleWidth: totalCandleWidth,
                        offset: offset + dragOffset,
                        chartWidth: chartWidth,
                        color: .green
                    )
                }

                ForEach(supplyZones) { zone in
                    ZoneOverlayView(
                        zone: zone,
                        candles: candles,
                        priceMin: range.min,
                        priceMax: range.max,
                        chartHeight: chartHeight,
                        totalCandleWidth: totalCandleWidth,
                        offset: offset + dragOffset,
                        chartWidth: chartWidth,
                        color: .red
                    )
                }

                // Candlesticks
                Canvas { context, size in
                    let effectiveOffset = offset + dragOffset
                    for (index, candle) in candles.enumerated() {
                        guard let high = candle.high, let low = candle.low,
                              let open = candle.open, let close = candle.close else { continue }

                        let x = CGFloat(index) * totalCandleWidth + effectiveOffset
                        let bodyW = candleWidth * effectiveScale

                        // Skip candles outside visible area
                        guard x + bodyW > 0 && x < chartWidth else { continue }

                        let yHigh = yPosition(price: high, min: range.min, max: range.max, height: chartHeight)
                        let yLow = yPosition(price: low, min: range.min, max: range.max, height: chartHeight)
                        let yOpen = yPosition(price: open, min: range.min, max: range.max, height: chartHeight)
                        let yClose = yPosition(price: close, min: range.min, max: range.max, height: chartHeight)

                        let isBullish = close >= open
                        let color: Color = isBullish ? .green : .red
                        let bodyTop = min(yOpen, yClose)
                        let bodyHeight = max(abs(yOpen - yClose), 1)

                        // Wick
                        let wickX = x + bodyW / 2
                        var wickPath = Path()
                        wickPath.move(to: CGPoint(x: wickX, y: yHigh))
                        wickPath.addLine(to: CGPoint(x: wickX, y: yLow))
                        context.stroke(wickPath, with: .color(color), lineWidth: 1)

                        // Body
                        let bodyRect = CGRect(x: x, y: bodyTop, width: bodyW, height: bodyHeight)
                        if isBullish {
                            context.stroke(Path(bodyRect), with: .color(color), lineWidth: 1)
                            // Hollow bullish candle
                        } else {
                            context.fill(Path(bodyRect), with: .color(color))
                        }
                    }
                }
                .frame(height: chartHeight)

                // Current price line
                if let lastCandle = candles.last, let close = lastCandle.close {
                    let y = yPosition(price: close, min: range.min, max: range.max, height: chartHeight)
                    Path { path in
                        path.move(to: CGPoint(x: 0, y: y))
                        path.addLine(to: CGPoint(x: chartWidth, y: y))
                    }
                    .stroke(style: StrokeStyle(lineWidth: 0.8, dash: [4, 3]))
                    .foregroundStyle(.blue.opacity(0.6))

                    // Price label
                    Text(formatPrice(close))
                        .font(.system(size: 9, design: .monospaced))
                        .foregroundStyle(.blue)
                        .padding(.horizontal, 3)
                        .background(.blue.opacity(0.1))
                        .clipShape(RoundedRectangle(cornerRadius: 2))
                        .position(x: chartWidth - 30, y: y - 8)
                }
            }
            .clipped()
            .gesture(
                DragGesture()
                    .updating($dragOffset) { value, state, _ in
                        state = value.translation.width
                    }
                    .onEnded { value in
                        offset += value.translation.width
                        clampOffset(chartWidth: chartWidth)
                    }
            )
            .gesture(
                MagnificationGesture()
                    .updating($magnification) { value, state, _ in
                        state = value
                    }
                    .onEnded { value in
                        scale = max(0.3, min(scale * value, 5.0))
                    }
            )
            .onAppear {
                // Scroll to show latest candles
                let totalWidth = CGFloat(candles.count) * totalCandleWidth
                if totalWidth > chartWidth {
                    offset = chartWidth - totalWidth
                }
            }
        }
    }

    private func yPosition(price: Double, min: Double, max: Double, height: CGFloat) -> CGFloat {
        let ratio = (price - min) / (max - min)
        return height * (1.0 - ratio)
    }

    private func clampOffset(chartWidth: CGFloat) {
        let totalWidth = CGFloat(candles.count) * totalCandleWidth
        let minOffset = chartWidth - totalWidth - 50
        let maxOffset: CGFloat = 50
        offset = Swift.min(maxOffset, Swift.max(minOffset, offset))
    }

    private func formatPrice(_ price: Double) -> String {
        if price < 10 {
            return String(format: "%.5f", price)
        } else if price < 1000 {
            return String(format: "%.3f", price)
        } else {
            return String(format: "%.2f", price)
        }
    }
}
