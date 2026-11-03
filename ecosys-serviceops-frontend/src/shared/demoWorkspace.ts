/** The platform-run demo workspace's fixed tenant id (see DemoWorkspace on the server). */
export const DEMO_TENANT_ID = 'de300000-0000-4000-8000-000000000001'

export const isDemoTenant = (tenantId: string | null | undefined) => tenantId?.toLowerCase() === DEMO_TENANT_ID
