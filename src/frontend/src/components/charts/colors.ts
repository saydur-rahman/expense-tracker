/**
 * SVG `stroke` and `fill` need a literal value, so the chart marks can't use the
 * Tailwind token classes the rest of the app does. These are the same three colours,
 * validated with the data-viz validator against both card surfaces (#f8fafc light,
 * #141d2e dark) for lightness band, chroma, colour-blind separation and 3:1 contrast
 * — one pair serves both themes.
 *
 * Green is consistently "money you still have", blue "money that has gone out",
 * red "trouble". **Re-run the validator if you change one.**
 */
export const SPENT_COLOR = '#2563eb'
export const LEFT_COLOR = '#16a34a'
export const OVER_COLOR = '#dc2626'
