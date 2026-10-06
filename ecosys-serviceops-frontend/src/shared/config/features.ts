/**
 * Switches for features whose screens exist but whose backend isn't finished.
 *
 * WhatsApp: the inbox and settings save to the database only — nothing is sent to or received from
 * Meta (no Cloud API calls, no /api/whatsapp/webhook), so messages showed "Sent" while never leaving.
 * Hidden everywhere until a real WhatsApp Business integration exists; flip to true to bring it back.
 */
export const WHATSAPP_ENABLED = false
