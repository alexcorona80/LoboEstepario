// Adaptación de wwwroot/maps.js de la web: mismo mapa, pero con mensajería WebView2
// (window.chrome.webview) en lugar de JS interop de Blazor.
(function () {
  'use strict';

  function toMeters(km) {
    return (typeof km === 'number' && isFinite(km) && km >= 0) ? km * 1000 : 5000;
  }
  function post(msg) {
    if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage(msg);
  }

  var map = L.map('map', { zoomControl: true, scrollWheelZoom: true }).setView([20.676, -103.349], 13);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
  }).addTo(map);

  var marker = L.circleMarker([20.676, -103.349], {
    radius: 8, color: '#ffffff', weight: 2, fillColor: '#e2a63d', fillOpacity: 1
  }).addTo(map);

  var circle = L.circle([20.676, -103.349], {
    radius: 2000, color: '#e2a63d', weight: 2, fillColor: '#e2a63d', fillOpacity: 0.15
  }).addTo(map);

  var markersLayer = L.layerGroup().addTo(map);

  map.on('click', function (e) {
    post({ type: 'center', lat: e.latlng.lat, lng: e.latlng.lng });
  });
  map.on('dragend', function () {
    var c = map.getCenter();
    post({ type: 'center', lat: c.lat, lng: c.lng });
  });
  window.addEventListener('resize', function () { map.invalidateSize(); });

  function setCenter(lat, lng) {
    if (!isFinite(lat) || !isFinite(lng)) return;
    var t = L.latLng(lat, lng);
    marker.setLatLng(t);
    circle.setLatLng(t);
  }

  var handlers = {
    setCenter: function (m) { setCenter(m.lat, m.lng); },
    setRadius: function (m) {
      var meters = toMeters(m.radiusKm);
      if (Math.abs(circle.getRadius() - meters) > 1) circle.setRadius(meters);
    },
    recenter: function (m) {
      if (!isFinite(m.lat) || !isFinite(m.lng)) return;
      setCenter(m.lat, m.lng);
      map.setView([m.lat, m.lng], Math.max(map.getZoom(), 13), { animate: true });
    },
    setMarkers: function (m) {
      markersLayer.clearLayers();
      (m.items || []).forEach(function (it) {
        var p = L.circleMarker([it.lat, it.lng], {
          radius: 5, color: '#ffffff', weight: 1, fillColor: '#4fb3a9', fillOpacity: 0.9
        });
        if (it.name) p.bindTooltip(it.name);
        p.addTo(markersLayer);
      });
    },
    invalidate: function () { map.invalidateSize(); }
  };

  window.chrome.webview.addEventListener('message', function (e) {
    var m = e.data;
    if (typeof m === 'string') { try { m = JSON.parse(m); } catch (x) { return; } }
    if (m && handlers[m.type]) handlers[m.type](m);
  });

  setTimeout(function () { map.invalidateSize(); post({ type: 'ready' }); }, 0);
})();
