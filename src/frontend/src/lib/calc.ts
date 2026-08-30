/**
 * Arithmetic in amount fields, so "635*3" can be typed instead of doing the sum first.
 *
 * Deliberately a hand-written tokeniser and parser rather than `eval` or `new Function`:
 * the input is user text, and this way the grammar is exactly `+ - * / ( )` over numbers
 * and nothing else — no identifiers, no property access, no way to reach anything.
 */

export type AmountReading =
  /** Nothing typed. */
  | { kind: 'empty' }
  /** A plain number — no arithmetic to show the user. */
  | { kind: 'value'; value: number }
  /** Arithmetic that worked out; `value` is the result. */
  | { kind: 'expression'; value: number }
  /** Typed something we can't make a number of. */
  | { kind: 'invalid' }

type Token =
  | { t: 'num'; v: number }
  | { t: 'op'; v: '+' | '-' | '*' | '/' }
  | { t: 'open' }
  | { t: 'close' }

function isDigit(ch: string) {
  return ch >= '0' && ch <= '9'
}

function tokenise(input: string): Token[] | null {
  const tokens: Token[] = []
  let i = 0

  while (i < input.length) {
    const ch = input[i]

    if (ch === ' ') {
      i++
      continue
    }

    if (isDigit(ch) || ch === '.') {
      let j = i
      let dots = 0
      while (j < input.length && (isDigit(input[j]) || input[j] === '.')) {
        if (input[j] === '.' && ++dots > 1) return null
        j++
      }
      const value = Number(input.slice(i, j))
      if (!Number.isFinite(value)) return null
      tokens.push({ t: 'num', v: value })
      i = j
      continue
    }

    if (ch === '+' || ch === '-' || ch === '*' || ch === '/') {
      tokens.push({ t: 'op', v: ch })
      i++
      continue
    }

    if (ch === '(') {
      tokens.push({ t: 'open' })
      i++
      continue
    }

    if (ch === ')') {
      tokens.push({ t: 'close' })
      i++
      continue
    }

    // Anything else — a letter, a stray symbol — makes the whole thing unreadable.
    return null
  }

  return tokens
}

/**
 * Recursive descent, so `*` and `/` bind tighter than `+` and `-` the way everyone expects.
 * Returns null for anything malformed rather than guessing at what was meant.
 */
function parse(tokens: Token[]): number | null {
  let pos = 0

  function expression(): number | null {
    let left = term()
    if (left === null) return null

    while (pos < tokens.length) {
      const token = tokens[pos]
      if (token.t !== 'op' || (token.v !== '+' && token.v !== '-')) break
      pos++
      const right = term()
      if (right === null) return null
      left = token.v === '+' ? left + right : left - right
    }

    return left
  }

  function term(): number | null {
    let left = factor()
    if (left === null) return null

    while (pos < tokens.length) {
      const token = tokens[pos]
      if (token.t !== 'op' || (token.v !== '*' && token.v !== '/')) break
      pos++
      const right = factor()
      if (right === null) return null
      if (token.v === '/') {
        // Dividing by nothing has no answer to show, so the field stays unreadable
        // rather than reporting Infinity.
        if (right === 0) return null
        left = left / right
      } else {
        left = left * right
      }
    }

    return left
  }

  function factor(): number | null {
    const token = tokens[pos]
    if (!token) return null

    if (token.t === 'op' && (token.v === '-' || token.v === '+')) {
      pos++
      const value = factor()
      return value === null ? null : token.v === '-' ? -value : value
    }

    if (token.t === 'num') {
      pos++
      return token.v
    }

    if (token.t === 'open') {
      pos++
      const value = expression()
      if (value === null) return null
      if (tokens[pos]?.t !== 'close') return null
      pos++
      return value
    }

    return null
  }

  const value = expression()
  // Trailing tokens mean it only parsed in part — "2+3)" is not 5.
  return value === null || pos !== tokens.length ? null : value
}

/** Money is stored to two decimals, so that is where a result is settled. */
function toMoney(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100
}

/**
 * Reads what the user typed in an amount field. Grouping commas are dropped, and × ÷ are
 * accepted alongside * / since a phone keypad offers them.
 */
export function readAmount(raw: string): AmountReading {
  const cleaned = raw.replace(/,/g, '').replace(/×/g, '*').replace(/÷/g, '/').trim()
  if (cleaned === '') return { kind: 'empty' }

  const tokens = tokenise(cleaned)
  if (tokens === null || tokens.length === 0) return { kind: 'invalid' }

  const value = parse(tokens)
  if (value === null || !Number.isFinite(value)) return { kind: 'invalid' }

  // A leading sign is part of the number, not a sum — "-5" is a value, "6-5" is arithmetic.
  const isArithmetic =
    tokens.some((token, index) => token.t === 'op' && index > 0) ||
    tokens.some((token) => token.t === 'open')

  return { kind: isArithmetic ? 'expression' : 'value', value: toMoney(value) }
}

/** The number to submit, or null when the field is empty or unreadable. */
export function amountValue(raw: string): number | null {
  const reading = readAmount(raw)
  return reading.kind === 'value' || reading.kind === 'expression' ? reading.value : null
}
