import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { notifyOnce } from './notifyOnce'

describe('notifyOnce', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('runs the callback the first time a key fires', () => {
    const fn = vi.fn()
    notifyOnce('alert-storm-key-1', fn)
    expect(fn).toHaveBeenCalledTimes(1)
  })

  it('suppresses a second call for the same key within the cooldown window', () => {
    const fn = vi.fn()
    notifyOnce('alert-storm-key-2', fn)
    notifyOnce('alert-storm-key-2', fn)
    notifyOnce('alert-storm-key-2', fn)
    expect(fn).toHaveBeenCalledTimes(1)
  })

  it('does not suppress a different key', () => {
    const fn = vi.fn()
    notifyOnce('alert-storm-key-3a', fn)
    notifyOnce('alert-storm-key-3b', fn)
    expect(fn).toHaveBeenCalledTimes(2)
  })

  it('fires again once the cooldown has elapsed', () => {
    const fn = vi.fn()
    notifyOnce('alert-storm-key-4', fn, 1000)
    vi.advanceTimersByTime(1001)
    notifyOnce('alert-storm-key-4', fn, 1000)
    expect(fn).toHaveBeenCalledTimes(2)
  })

  it('still suppresses just before the cooldown elapses', () => {
    const fn = vi.fn()
    notifyOnce('alert-storm-key-5', fn, 1000)
    vi.advanceTimersByTime(999)
    notifyOnce('alert-storm-key-5', fn, 1000)
    expect(fn).toHaveBeenCalledTimes(1)
  })
})
