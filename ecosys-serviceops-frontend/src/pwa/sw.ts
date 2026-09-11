/// <reference lib="webworker" />

declare const self: ServiceWorkerGlobalScope
interface SyncEvent extends ExtendableEvent { readonly tag: string }

self.addEventListener('install', (event) => {
  event.waitUntil(self.skipWaiting())
})

self.addEventListener('activate', (event) => {
  event.waitUntil(self.clients.claim())
})

self.addEventListener('fetch', (event) => {
  event.respondWith(fetch(event.request))
})

self.addEventListener('sync', (event: Event) => {
  const syncEvent = event as SyncEvent
  if (syncEvent.tag !== 'ecosys-sync') return
  syncEvent.waitUntil(self.clients.matchAll({ type: 'window' }).then((clients) =>
    Promise.all(clients.map((client) => client.postMessage({ type: 'ECOSYS_SYNC_REQUESTED' }))),
  ))
})
