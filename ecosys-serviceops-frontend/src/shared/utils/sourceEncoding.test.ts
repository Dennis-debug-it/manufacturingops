/// <reference types="node" />
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, expect, it } from 'vitest'

// Guards against text that renders as mojibake in the UI: files not saved as UTF-8 (a Windows-1252
// em dash once reached the date formatter this way and rendered as the U+FFFD replacement character),
// U+FFFD left behind by a lossy re-encode, UTF-8 read back as Windows-1252 (e.g. an em dash shown as
// "a-circumflex, euro, right-quote"), and HTML entities in JS strings, which React prints literally.
const root = join(process.cwd(), 'src')
const selfName = 'sourceEncoding.test.ts'
const extensions = ['.ts', '.tsx', '.css', '.json', '.html']
const decoder = new TextDecoder('utf-8', { fatal: true })

const checks: Array<{ label: string; pattern: RegExp }> = [
  { label: 'replacement character U+FFFD', pattern: /\uFFFD/ },
  { label: 'UTF-8 decoded as Windows-1252', pattern: /\u00E2\u20AC|\u00C3[\u0080-\u00BF]|\u00C2[\u0080-\u00BF\u00A0]|\u00F0\u0178/ },
  { label: 'HTML entity inside a JS string', pattern: /['"`][^'"`\n]*&(?:amp|lt|gt|quot|#39|#x27|nbsp|mdash|ndash|hellip);[^'"`\n]*['"`]/ },
]

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return sourceFiles(path)
    return extensions.some((ext) => name.endsWith(ext)) && name !== selfName ? [path] : []
  })
}

describe('frontend source encoding', () => {
  const files = sourceFiles(root)

  it('finds the source tree', () => {
    expect(files.length).toBeGreaterThan(100)
  })

  it('has only valid UTF-8 files without mojibake', () => {
    const problems: string[] = []
    for (const file of files) {
      let text: string
      try {
        text = decoder.decode(readFileSync(file))
      } catch {
        problems.push(`${relative(root, file)}: not valid UTF-8`)
        continue
      }
      text.split('\n').forEach((line, index) => {
        for (const { label, pattern } of checks) {
          if (pattern.test(line)) problems.push(`${relative(root, file)}:${index + 1}: ${label}`)
        }
      })
    }
    expect(problems).toEqual([])
  })
})
