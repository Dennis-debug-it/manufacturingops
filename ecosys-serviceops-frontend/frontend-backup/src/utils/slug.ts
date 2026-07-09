export function slugifyName(value: string) {
  return value
    .trim()
    .toLowerCase()
    .replace(/&/g, '-')
    .replace(/'/g, '')
    .replace(/[^a-z0-9-]+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-+|-+$/g, '')
}

export function isValidSlug(value: string, minimumLength = 3) {
  if (!value) return false
  if (value.length < minimumLength) return false
  return /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value)
}

export function getSlugValidationMessage(value: string, minimumLength = 3) {
  if (!value.trim()) return 'Workspace URL name is required.'
  if (value !== value.toLowerCase()) return 'Workspace URL name must use lowercase letters only.'
  if (value.includes(' ')) return 'Workspace URL name cannot contain spaces.'
  if (value.startsWith('-') || value.endsWith('-')) return 'Workspace URL name cannot start or end with a hyphen.'
  if (!/^[a-z0-9-]+$/.test(value)) return 'Workspace URL name can only contain lowercase letters, numbers, and hyphens.'
  if (value.length < minimumLength) return `Workspace URL name must be at least ${minimumLength} characters long.`
  if (!isValidSlug(value, minimumLength)) return 'Workspace URL name is not valid.'
  return null
}
