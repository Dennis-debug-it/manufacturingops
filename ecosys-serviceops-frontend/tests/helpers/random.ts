function randomToken(length = 6) {
  return Math.random().toString(36).slice(2, 2 + length)
}

export function uniqueSuffix() {
  return `${Date.now()}-${randomToken(8)}`
}

export function buildSignupData() {
  const suffix = uniqueSuffix()
  return {
    companyName: `Ecosys QA ${suffix}`,
    fullName: `QA Admin ${suffix}`,
    email: `qa.${suffix}@example.com`,
    password: process.env.E2E_SIGNUP_PASSWORD || 'Ecosys!23456',
    industry: 'Facilities',
    country: 'Kenya',
  }
}

