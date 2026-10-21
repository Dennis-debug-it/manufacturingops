import { describe, expect, it } from 'vitest'
import {
  cleanVerificationCode,
  codeTimeLeft,
  consentMethodLabel,
  formatWhatsAppNumber,
  normalizeWhatsAppNumber,
  outboxStatusLabel,
  toggleAllowedKey,
  usagePercent,
} from './reminderLogic'

describe('normalizeWhatsAppNumber', () => {
  it.each([
    ['0712 345 678', '+254712345678'],
    ['0712-345-678', '+254712345678'],
    ['+254 712 345 678', '+254712345678'],
    ['254712345678', '+254712345678'],
    ['712345678', '+254712345678'],
    ['0110 123 456', '+254110123456'],
    ['00254712345678', '+254712345678'],
    ['+44 7911 123456', '+447911123456'],
  ])('reads %s as %s', (input, expected) => {
    expect(normalizeWhatsAppNumber(input)).toBe(expected)
  })

  it('uses the workspace country for a leading 0', () => {
    expect(normalizeWhatsAppNumber('0772 123 456', '256')).toBe('+256772123456')
  })

  it.each(['', '07123', '0612 345 678', '0712 345 678 9', 'call me', '+0712345678'])('rejects %j', (input) => {
    expect(normalizeWhatsAppNumber(input)).toBeNull()
  })
})

describe('display helpers', () => {
  it('groups Kenyan numbers and leaves others alone', () => {
    expect(formatWhatsAppNumber('+254712345678')).toBe('+254 712 345 678')
    expect(formatWhatsAppNumber('+447911123456')).toBe('+447911123456')
    expect(formatWhatsAppNumber(null)).toBe('')
  })

  it('keeps only six digits of a code', () => {
    expect(cleanVerificationCode(' 12 34-567 ')).toBe('123456')
  })

  it('counts down the code and stops at zero', () => {
    const now = new Date('2026-10-21T10:00:00Z')
    expect(codeTimeLeft('2026-10-21T10:09:05Z', now)).toBe('9:05')
    expect(codeTimeLeft('2026-10-21T09:59:00Z', now)).toBe('0:00')
  })

  it('names statuses and consent methods plainly', () => {
    expect(outboxStatusLabel('Skipped')).toBe('Not sent')
    expect(outboxStatusLabel('Read')).toBe('Read')
    expect(consentMethodLabel('InWriting')).toBe('In writing')
    expect(consentMethodLabel('Portal')).toBe('Confirmed from the portal')
    expect(consentMethodLabel(null)).toBe('—')
  })
})

describe('settings helpers', () => {
  it('toggles a key without duplicates', () => {
    expect(toggleAllowedKey(['work-order.assigned'], 'project.muster-missing', true)).toEqual(['project.muster-missing', 'work-order.assigned'])
    expect(toggleAllowedKey(['a', 'a', 'b'], 'a', false)).toEqual(['b'])
    expect(toggleAllowedKey(['a'], 'a', true)).toEqual(['a'])
  })

  it('caps usage at 100%', () => {
    expect(usagePercent(250, 1000)).toBe(25)
    expect(usagePercent(1200, 1000)).toBe(100)
    expect(usagePercent(1, 0)).toBe(100)
  })
})
