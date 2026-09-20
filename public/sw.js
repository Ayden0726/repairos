self.addEventListener("install", (event) => {
  event.waitUntil(caches.open("workshopos-shell-v1").then((cache) => cache.addAll(["/offline.html"])));
});

self.addEventListener("fetch", (event) => {
  if (event.request.method !== "GET") return;
  event.respondWith(
    fetch(event.request).catch(() => caches.match("/offline.html")),
  );
});
