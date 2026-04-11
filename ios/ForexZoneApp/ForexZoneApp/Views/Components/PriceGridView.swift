import SwiftUI

/// Horizontal price grid lines with labels
struct PriceGridView: View {
    let priceMin: Double
    let priceMax: Double
    let chartHeight: CGFloat
    let chartWidth: CGFloat

    private var gridLines: [Double] {
        let range = priceMax - priceMin
        guard range > 0 else { return [] }

        // Choose a nice step size
        let rawStep = range / 6
        let magnitude = pow(10, floor(log10(rawStep)))
        let normalized = rawStep / magnitude
        let niceStep: Double
        if normalized <= 1.5 { niceStep = 1 * magnitude }
        else if normalized <= 3.5 { niceStep = 2 * magnitude }
        else if normalized <= 7.5 { niceStep = 5 * magnitude }
        else { niceStep = 10 * magnitude }

        let start = ceil(priceMin / niceStep) * niceStep
        var lines: [Double] = []
        var price = start
        while price < priceMax {
            lines.append(price)
            price += niceStep
        }
        return lines
    }

    var body: some View {
        ForEach(gridLines, id: \.self) { price in
            let y = yPosition(price: price)
            Path { path in
                path.move(to: CGPoint(x: 0, y: y))
                path.addLine(to: CGPoint(x: chartWidth, y: y))
            }
            .stroke(Color.gray.opacity(0.15), lineWidth: 0.5)

            Text(formatPrice(price))
                .font(.system(size: 8, design: .monospaced))
                .foregroundStyle(.secondary)
                .position(x: chartWidth - 28, y: y)
        }
    }

    private func yPosition(price: Double) -> CGFloat {
        let ratio = (price - priceMin) / (priceMax - priceMin)
        return chartHeight * (1.0 - ratio)
    }

    private func formatPrice(_ price: Double) -> String {
        if price < 10 {
            return String(format: "%.4f", price)
        } else if price < 1000 {
            return String(format: "%.2f", price)
        } else {
            return String(format: "%.1f", price)
        }
    }
}
