import SwiftUI

struct CrosshairOverlay: View {
    let candles: [Candle]
    let crosshairPosition: CGPoint?
    let isDraggingCrosshair: Bool
    let chartWidth: CGFloat
    let chartHeight: CGFloat
    let priceAxisWidth: CGFloat
    let timeAxisHeight: CGFloat
    let priceRange: (min: Double, max: Double)
    let totalCandleWidth: CGFloat
    let effectiveOffset: CGFloat

    var body: some View {
        ZStack(alignment: .topLeading) {
            currentPriceLine
            crosshairLines
        }
    }

    @ViewBuilder
    private var currentPriceLine: some View {
        if let lastCandle = candles.last, let close = lastCandle.close {
            let y = yPos(close)
            Path { path in
                path.move(to: CGPoint(x: 0, y: y))
                path.addLine(to: CGPoint(x: chartWidth, y: y))
            }
            .stroke(style: StrokeStyle(lineWidth: 0.8, dash: [4, 3]))
            .foregroundStyle(.blue.opacity(0.6))

            Text(formatPrice(close))
                .font(.system(size: 8, design: .monospaced))
                .foregroundStyle(.white)
                .padding(.horizontal, 3)
                .padding(.vertical, 1)
                .background(.blue)
                .clipShape(RoundedRectangle(cornerRadius: 2))
                .position(x: chartWidth + priceAxisWidth / 2, y: y)
        }
    }

    @ViewBuilder
    private var crosshairLines: some View {
        if let pos = crosshairPosition, isDraggingCrosshair,
           pos.x >= 0, pos.x <= chartWidth, pos.y >= 0, pos.y <= chartHeight {

            // Vertical line
            Path { p in
                p.move(to: CGPoint(x: pos.x, y: 0))
                p.addLine(to: CGPoint(x: pos.x, y: chartHeight))
            }
            .stroke(style: StrokeStyle(lineWidth: 0.6, dash: [3, 2]))
            .foregroundStyle(.white.opacity(0.7))

            // Horizontal line
            Path { p in
                p.move(to: CGPoint(x: 0, y: pos.y))
                p.addLine(to: CGPoint(x: chartWidth, y: pos.y))
            }
            .stroke(style: StrokeStyle(lineWidth: 0.6, dash: [3, 2]))
            .foregroundStyle(.white.opacity(0.7))

            // Price label
            crosshairPriceLabel(pos: pos)

            // Time label
            crosshairTimeLabel(pos: pos)

            // Center dot
            Circle()
                .fill(.orange)
                .frame(width: 6, height: 6)
                .position(pos)
        }
    }

    private func crosshairPriceLabel(pos: CGPoint) -> some View {
        let price = priceAtY(pos.y)
        return Text(formatPrice(price))
            .font(.system(size: 8, design: .monospaced))
            .foregroundStyle(.white)
            .padding(.horizontal, 3)
            .padding(.vertical, 1)
            .background(.orange)
            .clipShape(RoundedRectangle(cornerRadius: 2))
            .position(x: chartWidth + priceAxisWidth / 2, y: pos.y)
    }

    @ViewBuilder
    private func crosshairTimeLabel(pos: CGPoint) -> some View {
        let idx = candleIndexAtX(pos.x)
        if idx >= 0, idx < candles.count, let date = candles[idx].date {
            let fmt = DateFormatter()
            let _ = fmt.dateFormat = "dd MMM HH:mm"
            Text(fmt.string(from: date))
                .font(.system(size: 8, design: .monospaced))
                .foregroundStyle(.white)
                .padding(.horizontal, 4)
                .padding(.vertical, 2)
                .background(.orange)
                .clipShape(RoundedRectangle(cornerRadius: 2))
                .position(x: pos.x, y: chartHeight + timeAxisHeight / 2)
        }
    }

    // MARK: - Helpers

    private func yPos(_ price: Double) -> CGFloat {
        let ratio = (price - priceRange.min) / (priceRange.max - priceRange.min)
        return chartHeight * (1.0 - ratio)
    }

    private func priceAtY(_ y: CGFloat) -> Double {
        let ratio = 1.0 - Double(y / chartHeight)
        return priceRange.min + ratio * (priceRange.max - priceRange.min)
    }

    private func candleIndexAtX(_ x: CGFloat) -> Int {
        let raw = Int((x - effectiveOffset) / totalCandleWidth)
        return max(0, min(raw, candles.count - 1))
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
