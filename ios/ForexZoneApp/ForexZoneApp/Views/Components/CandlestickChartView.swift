import SwiftUI

struct CandlestickChartView: View {
    let candles: [Candle]
    let supplyZones: [Zone]
    let demandZones: [Zone]
    @Binding var focusedZone: Zone?
    var onZoneTapped: ((Zone) -> Void)?

    @State private var offset: CGFloat = 0
    @State private var scale: CGFloat = 1.0
    @GestureState private var dragOffset: CGFloat = 0
    @GestureState private var magnification: CGFloat = 1.0

    // Crosshair state
    @State private var crosshairPosition: CGPoint?
    @State private var isDraggingCrosshair = false

    private let candleWidth: CGFloat = 8
    private let candleSpacing: CGFloat = 3
    private let timeAxisHeight: CGFloat = 28
    private let priceAxisWidth: CGFloat = 58

    private static let timeAxisFmt: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "dd MMM\nHH:mm"
        return f
    }()

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
            let chartHeight = geo.size.height - timeAxisHeight
            let chartWidth = geo.size.width - priceAxisWidth

            ZStack(alignment: .topLeading) {
                // Dark chart background
                Color(red: 0.08, green: 0.09, blue: 0.12)

                // Price grid
                PriceGridView(
                    priceMin: range.min,
                    priceMax: range.max,
                    chartHeight: chartHeight,
                    chartWidth: chartWidth
                )

                // Zone overlays
                ForEach(demandZones) { zone in
                    ZoneOverlayView(
                        zone: zone, candles: candles,
                        priceMin: range.min, priceMax: range.max,
                        chartHeight: chartHeight, totalCandleWidth: totalCandleWidth,
                        offset: offset + dragOffset, chartWidth: chartWidth,
                        color: .green
                    )
                }
                ForEach(supplyZones) { zone in
                    ZoneOverlayView(
                        zone: zone, candles: candles,
                        priceMin: range.min, priceMax: range.max,
                        chartHeight: chartHeight, totalCandleWidth: totalCandleWidth,
                        offset: offset + dragOffset, chartWidth: chartWidth,
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
                        guard x + bodyW > 0 && x < chartWidth else { continue }

                        let yHigh = yPos(high, range, chartHeight)
                        let yLow = yPos(low, range, chartHeight)
                        let yOpen = yPos(open, range, chartHeight)
                        let yClose = yPos(close, range, chartHeight)

                        let isBullish = close >= open
                        let color: Color = isBullish ? .green : .red
                        let bodyTop = min(yOpen, yClose)
                        let bodyHeight = max(abs(yOpen - yClose), 1)

                        var wickPath = Path()
                        wickPath.move(to: CGPoint(x: x + bodyW / 2, y: yHigh))
                        wickPath.addLine(to: CGPoint(x: x + bodyW / 2, y: yLow))
                        context.stroke(wickPath, with: .color(color), lineWidth: 1)

                        let bodyRect = CGRect(x: x, y: bodyTop, width: bodyW, height: bodyHeight)
                        if isBullish {
                            context.stroke(Path(bodyRect), with: .color(color), lineWidth: 1)
                        } else {
                            context.fill(Path(bodyRect), with: .color(color))
                        }
                    }
                }
                .frame(width: chartWidth, height: chartHeight)
                .drawingGroup() // Flatten to Metal for better perf
                Canvas { context, size in
                    let effectiveOffset = offset + dragOffset
                    let stepValue: CGFloat = 60.0 / totalCandleWidth
                    let labelStep: Int = max(1, Int(stepValue))
                    let bodyW: CGFloat = candleWidth * effectiveScale

                    for i in stride(from: 0, to: candles.count, by: labelStep) {
                        let x: CGFloat = CGFloat(i) * totalCandleWidth + effectiveOffset + bodyW / 2.0
                        guard x > 0 && x < chartWidth else { continue }

                        if let date = parseISO8601(candles[i].time) {
                            let text = Text(Self.timeAxisFmt.string(from: date))
                                .font(.system(size: 7, design: .monospaced))
                                .foregroundStyle(.secondary)
                            context.draw(
                                context.resolve(text),
                                at: CGPoint(x: x, y: size.height / 2),
                                anchor: .center
                            )
                        }
                    }
                }
                .frame(width: chartWidth, height: timeAxisHeight)
                .offset(y: chartHeight)

                // Price axis labels (right side)
                Canvas { context, size in
                    let steps = max(2, Int(chartHeight / 50))
                    for i in 0...steps {
                        let ratio = Double(i) / Double(steps)
                        let price = range.max - ratio * (range.max - range.min)
                        let y = chartHeight * ratio
                        let text = Text(formatPrice(price))
                            .font(.system(size: 8, design: .monospaced))
                            .foregroundStyle(.secondary)
                        context.draw(
                            context.resolve(text),
                            at: CGPoint(x: priceAxisWidth / 2, y: y),
                            anchor: .center
                        )
                    }
                }
                .frame(width: priceAxisWidth, height: chartHeight)
                .offset(x: chartWidth)

                // Current price line + crosshair
                CrosshairOverlay(
                    candles: candles,
                    crosshairPosition: crosshairPosition,
                    isDraggingCrosshair: isDraggingCrosshair,
                    chartWidth: chartWidth,
                    chartHeight: chartHeight,
                    priceAxisWidth: priceAxisWidth,
                    timeAxisHeight: timeAxisHeight,
                    priceRange: range,
                    totalCandleWidth: totalCandleWidth,
                    effectiveOffset: offset + dragOffset
                )
            }
            .clipped()
            .contentShape(Rectangle())
            .gesture(
                LongPressGesture(minimumDuration: 0.2)
                    .sequenced(before: DragGesture(minimumDistance: 0))
                    .onChanged { value in
                        switch value {
                        case .second(true, let drag):
                            isDraggingCrosshair = true
                            if let drag = drag {
                                crosshairPosition = drag.location
                            }
                        default:
                            break
                        }
                    }
                    .onEnded { _ in
                        isDraggingCrosshair = false
                        crosshairPosition = nil
                    }
            )
            .simultaneousGesture(
                isDraggingCrosshair ? nil :
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
                let totalWidth = CGFloat(candles.count) * totalCandleWidth
                if totalWidth > chartWidth {
                    offset = chartWidth - totalWidth
                }
            }
            .onChange(of: focusedZone?.id) { _, _ in
                guard let zone = focusedZone else { return }
                let index = findCandleIndex(for: zone.startTime)
                let targetX = CGFloat(index) * totalCandleWidth
                withAnimation(.easeInOut(duration: 0.4)) {
                    offset = chartWidth / 2 - targetX
                    clampOffset(chartWidth: chartWidth)
                }
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                    focusedZone = nil
                }
            }
            .simultaneousGesture(
                SpatialTapGesture()
                    .onEnded { event in
                        guard onZoneTapped != nil else { return }
                        let tapY = event.location.y
                        guard tapY < chartHeight else { return }
                        let price = range.max - (Double(tapY) / Double(chartHeight)) * (range.max - range.min)
                        let allZones = supplyZones + demandZones
                        if let hit = allZones.first(where: { price >= $0.baseRangeLow && price <= $0.baseRangeHigh }) {
                            onZoneTapped?(hit)
                        }
                    }
            )
        }
    }

    // MARK: - Helpers

    private func yPos(_ price: Double, _ range: (min: Double, max: Double), _ height: CGFloat) -> CGFloat {
        let ratio = (price - range.min) / (range.max - range.min)
        return height * (1.0 - ratio)
    }

    private func clampOffset(chartWidth: CGFloat) {
        let totalWidth = CGFloat(candles.count) * totalCandleWidth
        let minOffset = chartWidth - totalWidth - 50
        let maxOffset: CGFloat = 50
        offset = Swift.min(maxOffset, Swift.max(minOffset, offset))
    }

    private func findCandleIndex(for timeStr: String) -> Int {
        guard let target = parseISO8601(timeStr) else { return 0 }
        for (index, candle) in candles.enumerated() {
            if let candleDate = parseISO8601(candle.time), candleDate >= target {
                return max(0, index - 1)
            }
        }
        return max(0, candles.count - 1)
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
