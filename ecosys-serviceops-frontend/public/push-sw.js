/* Imported by the generated service worker (vite.config.ts → workbox.importScripts).
 * The API sends { title, body, url, tag } — currently only "new job assigned" (WebPush.cs). */
self.addEventListener('push', (event) => {
  let data = {}
  try {
    data = event.data ? event.data.json() : {}
  } catch {
    data = { body: event.data ? event.data.text() : '' }
  }
  event.waitUntil(
    self.registration.showNotification(data.title || 'Ecosys', {
      body: data.body || '',
      tag: data.tag,
      renotify: Boolean(data.tag),
      icon: '/pwa-192x192.png',
      badge: '/pwa-192x192.png',
      data: { url: data.url || '/tech' },
    }),
  )
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const target = new URL(event.notification.data?.url || '/tech', self.location.origin)
  if (target.origin !== self.location.origin) return
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      const open = windows.find((client) => new URL(client.url).origin === target.origin)
      if (open) {
        return open.focus().then((client) => (client && 'navigate' in client ? client.navigate(target.href) : undefined))
      }
      return self.clients.openWindow(target.href)
    }),
  )
})
