export interface PortalUser {
  portalUserId: string
  fullName: string
  email: string
}

export interface PortalClient {
  clientId: string
  clientName: string
}

export interface PortalTenant {
  tenantId: string
  companyName: string
}

export interface PortalSession {
  token: string
  user: PortalUser
  client: PortalClient
  tenant: PortalTenant
}

export interface PortalVerifyResponse {
  token: string
  user: PortalUser
  client: PortalClient
  tenant: PortalTenant
}
